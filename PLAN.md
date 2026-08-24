# 屏幕分辨率切换悬浮工具 — 设计文档（C# / .NET 10 版）

> 技术方案已从 Python 切换为 **C# (.NET 10 + WinForms)**。本地已确认安装 .NET SDK 10.0.111。
> 本文件是历史设计稿；当前实现细节见 `doc/impl-notes/` 与 `doc/PROJECT.md`。

## 1. 概述

Windows 常驻后台小工具：屏幕上显示一个可拖拽的半透明悬浮按钮，单击一键切换指定显示器分辨率（如 3440x1440 ↔ 2560x1440），右键弹出菜单可打开设置或退出。支持开机自启，交付为单个 exe，双击直接运行，无控制台窗口、无 bat/ps1 残留。

## 2. 技术选型

| 项目 | 选择 | 理由 |
|---|---|---|
| 语言/运行时 | C# / .NET 10 (`net10.0-windows`) | 本地 SDK 已就绪；启动快、体积小、杀软误报少 |
| UI 框架 | WinForms | 无边框/透明/置顶窗体原生支持；`Timer` 驱动透明度动画简单可靠 |
| Win32 调用 | P/Invoke（`LibraryImport`）调用 `user32.dll` | `EnumDisplaySettingsExW` 枚举模式、`ChangeDisplaySettingsExW` 切换分辨率 |
| 配置存储 | JSON（`System.Text.Json`）存于 `%APPDATA%\ResSwitcher\config.json` | 简单可手改 |
| 开机自启 | 注册表 `HKCU\...\Run`（`Microsoft.Win32.Registry`） | 标准方式，无需提权 |
| 打包 | `dotnet publish` 单文件发布 | 一条命令出 exe，无脚本残留 |

## 3. 项目结构

```
desktop-helper-tools/
├── ResSwitcher.sln
├── src/ResSwitcher/
│   ├── ResSwitcher.csproj          # net10.0-windows, WinForms, PerMonitorV2
│   ├── Program.cs                  # 入口：单实例互斥 → 启动
│   ├── AppContext.cs               # 全局组合根：持有配置/服务并装配
│   ├── Core/
│   │   ├── DisplayApi.cs           # P/Invoke：枚举显示器与模式、切换分辨率
│   │   ├── ResolutionSwitcher.cs   # 单/双模式切换状态机
│   │   ├── AppConfig.cs            # 配置模型 + 读写
│   │   └── AutostartManager.cs     # HKCU Run 注册表管理
│   └── Ui/
│       ├── OverlayForm.cs          # 悬浮按钮窗体
│       └── SettingsForm.cs         # 设置界面
└── doc/                            # 当前实现笔记与项目规范
```

依赖方向：`Program` → `AppContext` → `Ui.*` 与 `Core.*`；`Ui` 只依赖 `Core` 接口/模型，禁止反向引用。

## 4. 关键设计

### 4.1 悬浮按钮（OverlayForm）

- `FormBorderStyle.None` + `TopMost=true` + `ShowInTaskbar=false` + `StartPosition=Manual`。
- 圆形外观：背景色设为 `TransparencyKey`，用 `Paint` 事件画圆形按钮。
- **拖拽 vs 点击**：`MouseDown` 记录起点；`MouseMove` 位移 > 4px 进入拖拽态跟随移动；`MouseUp` 未超阈值视为单击 → 触发切换。
- **透明度动画**：`Timer`(50ms)。拖拽中 `Opacity=1.0`；松手后线性衰减至配置的静止透明度（默认 0.35）。
- **位置记忆**：每次拖拽停止（及退出）时保存坐标到配置，下次启动自动恢复到上次位置；无记录或位置无效时默认出现在**屏幕 1 的右上角**（贴边留 16px 边距）；启动时做屏幕边界钳制（防止分辨率切换后按钮跑出屏外）。
- **右键菜单**：`ContextMenuStrip`：设置… / 退出。

### 4.2 显示 API（DisplayApi）

```csharp
EnumDisplaySettingsExW(deviceName, iModeNum, ref DEVMODEW, flags) // iModeNum=-1 取当前模式
ChangeDisplaySettingsExW(deviceName, ref DEVMODEW, 0, CDS_UPDATEREGISTRY, 0)
EnumDisplayDevicesW(...)   // 枚举 "\\.\DISPLAYn" 及其友好名称
```

- 切换前校验目标 `(width,height)` 在该显示器支持的模式列表中，不支持则返回失败不切换。
- 进程级 DPI 感知：csproj 中 `ApplicationHighDpiMode=PerMonitorV2`。

### 4.3 切换状态机（ResolutionSwitcher）

```
单分辨率模式:  current == target ? original : target      （来回切换）

双分辨率模式:  state ∈ {AT_1, AT_2, OTHER}
    OTHER → 点击 → res1 → AT_1
    AT_1  → 点击 → res2  → AT_2
    AT_2  → 点击 → res1  → AT_1   （永不回到 OTHER 的原始分辨率）
```

每次点击后读取实际生效分辨率更新状态（以系统反馈为准）。

### 4.4 设置界面（SettingsForm）

模态对话框：☑ 开机自启；目标显示器下拉框；按钮透明度滑条 / 颜色 / 大小；模式单选（单/双）及对应分辨率输入；确定后热更新悬浮按钮并保存配置。

### 4.5 配置模型（AppConfig）

```json
{
  "Monitor": "\\\\.\\DISPLAY1",
  "Mode": "dual",
  "Single": { "Width": 2560, "Height": 1440 },
  "Dual":   { "Res1": [3440,1440], "Res2": [2560,1440] },
  "Button": { "X": 100, "Y": 800, "Size": 48, "Color": "#3B82F6", "IdleAlpha": 0.35 },
  "Autostart": false
}
```

首次运行生成默认值；读取失败回退默认。

### 4.6 开机自启（AutostartManager）

勾选写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 值 `ResSwitcher = "<exe路径>"`；取消删除。仅 HKCU。

## 5. 打包

```
dotnet publish src/ResSwitcher -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:SelfContained=false `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

产物 `bin/Release/net10.0-windows/win-x64/publish/ResSwitcher.exe`。本机已装 .NET 10 运行时故用框架依赖以减小体积；如需分发到无运行时机器，把 `-p:SelfContained=true` 即可。

## 6. 边界与异常处理

| 场景 | 处理 |
|---|---|
| 目标分辨率不被支持 | 提示消息框，不执行切换 |
| 多显示器 / 热插拔 | 打开设置时重新枚举；切换失败重试一次后提示 |
| 配置损坏 | 回退默认配置 |
| 重复启动 | 命名 `Mutex("Local\\ResSwitcher")`，已存在则退出 |
| 退出 | 保存按钮位置后关闭 |

## 7. 里程碑

当前实现笔记见 `doc/impl-notes/`；行为规格与验收清单见 `doc/PROJECT.md`
