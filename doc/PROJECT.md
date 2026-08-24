# ResSwitcher 项目文档

> 本文档是项目的行为规格与仓库约束参考。实现细节按领域整理在 [`ai-implementation-notes.md`](ai-implementation-notes.md) 及 `impl-notes/` 下。UI 框架为 **WPF**（2026-08 从 WinForms 迁移，Core 层零改动）

## 1. 项目概述

Windows 常驻后台小工具：屏幕上有一个可拖拽的半透明圆角长方形悬浮按钮（左半区切换主显示器、右半区切换分辨率），右键打开设置或退出。支持开机自启。

- 技术栈：C# / .NET 10 (`net10.0-windows`) / **WPF**
- 运行时依赖：仅 .NET 10 Desktop Runtime（发布为框架依赖单文件 exe）
- 配置位置：`%APPDATA%\ResSwitcher\config.json`
- 日志位置：`%APPDATA%\ResSwitcher\logs\reswitcher-YYYYMMDD-HHmmss-GUID.log`，每次进程会话单独一个文件；写入日志时自动删除最后写入时间超过 3 天的日志
- 自启方式：注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

## 2. 仓库结构

```
desktop-helper-tools/
├── .agents/skills/             # ResSwitcher 专用 agent 工作流
│   ├── consolidate-note/       # doc 实现笔记整理
│   ├── consolidate-todo/       # roadmap/TODO 维护（按需）
│   ├── review-fix-items/       # 静态审查与修复循环
│   └── update-instructions/    # AGENTS.md 安全提案流程
├── ResSwitcher.lnk            # 程序快捷方式（指向 dist\ResSwitcher.exe）
├── ResSwitcher-Build.lnk      # 发布流程快捷方式（运行 build-release.ps1）
├── build-release.ps1          # 一键发布：构建→测试→发布到 dist\
├── create-shortcut.ps1        # 快捷方式创建（幂等，丢失后重跑恢复）
├── README.md                  # 面向使用者的英文说明
├── README.zh.md               # 面向使用者的中文说明
├── doc/
│   ├── PROJECT.md             # 行为规格与仓库约束
│   ├── ai-implementation-notes.md # agent-facing 实现笔记索引
│   └── impl-notes/            # 按领域拆分的实现笔记
├── src/ResSwitcher/
│   ├── ResSwitcher.csproj     # WinExe / UseWPF / InternalsVisibleTo.Tests
│   ├── Program.cs             # 入口：单实例 Mutex → AppContext.Run()
│   ├── AppContext.cs          # WPF Application 组合根：装配配置/状态机/悬浮窗/设置窗
│   ├── Core/                  # 纯逻辑层（禁止引用任何 UI 框架类型）
│   │   ├── DisplayApi.cs      # 全部 P/Invoke 唯一所在；枚举显示器/模式/几何、切分辨率、切主屏
│   │   ├── ResolutionSwitcher.cs  # 切换状态机 + MonitorTarget 解析器（internal 委托可注入 fake）
│   │   ├── AppConfig.cs       # 配置模型 + AppConfigStore（JSON 原子读写）
│   │   └── AutostartManager.cs    # HKCU Run 注册表管理
│   └── Ui/                    # WPF 层
│       ├── OverlayWindow.cs   # 悬浮圆角窗：AllowsTransparency、双分区、拖拽、位置记忆
│       ├── SettingsWindow.cs  # 设置窗口（纯代码构建，无 XAML）
│       └── TrayIcon.cs        # 通知区域图标与托盘消息入口
├── tests/ResSwitcher.Tests/   # xunit 测试工程（唯一允许第三方包的地方）
└── dist/ResSwitcher.exe       # 发布产物
```

## 3. 核心行为规格

### 3.1 悬浮按钮交互

| 操作 | 行为 |
|---|---|
| 左键按下并移动 >4px | 进入拖拽：跟随鼠标，透明度动画到 1.0 |
| 左键原地按下抬起（≤4px） | 按分区触发：**左半区=切换主显示器，右半区=切换分辨率** |
| 鼠标悬停 | 整个按钮立即显现为完全不透明，该分区半透明白高亮 |
| 松手 | 透明度 600ms 缓动淡出至静止值（默认 0.35） |
| 右键 | 菜单：设置… / 退出 |
| 通知区域图标左键 | 打开设置窗口 |
| 通知区域图标右键 | 菜单：设置… / 退出 |

### 3.2 切换状态机

**分辨率（右区）**——
未配置或配置列表为空时返回 `NotConfigured`，提示用户打开设置选择分辨率。
分辨率列表按显示器 profile 独立保存；每次切换前读取目标显示器支持列表，profile 中不支持的项目按不存在处理，不会提交给系统。
单模式：`current == target ? original : target`（original 首次采样；启动即在 target 时取支持列表像素数最大的其他模式）。
双模式：`==res1→res2；==res2→res1；其他→res1`（无显式状态，实时 current 决定，永不回原始）

**主屏（左区）**——
`DisplayApi.TrySetPrimaryMonitor`：优先通过 `QueryDisplayConfig` 读取完整活动拓扑，将当前主屏之外的目标 source 放到 `(0,0)`、其他 source 同步平移并把目标路径置首，再以 `SetDisplayConfig` 验证并保存应用。失败时回退到 legacy `ChangeDisplaySettingsExW`，实际应用仍取决于显示驱动兼容性。Windows 会整体平移虚拟桌面以保持屏幕相对位置，应用层按同一偏移补偿悬浮窗，保持其所在物理屏幕和相对位置。主屏切换与设置中的分辨率目标解耦；无论分辨率目标是 `auto` 还是固定显示器，左区都在当前主屏与另一块活动显示器之间互换。`auto` 仅在右区分辨率切换时解析当前主屏，因此主屏互换后右区会跟随新的主屏。

**MonitorTarget 解析**：配置值 `"auto"` → 主屏（由 `MONITORINFOF_PRIMARY` 标志确定）；否则为固定设备名

### 3.3 位置记忆

拖拽停止与退出时保存坐标；启动恢复。无记录（`int.MinValue` 哨兵）或完全出屏时默认主屏右上角贴边 16px。**负坐标合法**（副屏在主屏左侧）

### 3.4 设置项

开机自启 / 目标显示器 / 按钮大小(24–128px) / 静止透明度(0.1–1.0) / 按钮颜色（内置色板循环）/ 分辨率列表。选择显示器后编辑该显示器的 profile；已配置但不支持的分辨率灰显，新增下拉只列出当前显示器支持的分辨率。

## 4. 配置文件格式

```json
{
  "Monitor": "auto",
  "Collection": { "Items": [[3440, 1440], [2560, 1440]] },
  "MonitorProfiles": [
    { "DisplayId": "MONITOR\\GSM7768\\...", "DisplayName": "主屏", "Items": [[3440, 1440], [2560, 1440]] },
    { "DisplayId": "MONITOR\\ACME123\\...", "DisplayName": "副屏", "Items": [[1920, 1080]] }
  ],
  "Single": { "Width": 2560, "Height": 1440 },
  "Button": { "X": -176, "Y": 110, "Size": 48, "Color": "#3B82F6", "IdleAlpha": 0.35 },
  "Autostart": false
}
```

X/Y 为 `int.MinValue`(-2147483648) 表示无记录。`MonitorProfiles` 优先于旧版全局 `Collection`；旧配置首次打开设置时迁移到当时选中的目标显示器。显示器 profile 优先使用稳定设备 ID，无法读取时回退设备名。损坏自动回退默认；.tmp+Replace 原子写

## 5. 构建与测试

```powershell
dotnet build -c Release                          # 必须 0 error 0 warning
dotnet test tests/ResSwitcher.Tests -c Release   # 41 用例
.\build-release.ps1                              # 一键发布到 dist\
```

## 6. 测试体系

自动化（离线、注入 fake）：D1–D14 切换与主屏、C1–C7 配置、A1–A3 自启、L1–L6 日志与显示 API、E1–E3 错误上下文、CCD 索引解码和几何变换，以及托盘原生命令映射用例，共 41 个。L1–L2 覆盖 session 文件命名、异常堆栈和三天前日志清理；D11–D12 覆盖按显示器 profile 隔离与不支持项过滤；D13–D14 覆盖主屏切换与分辨率目标解耦及 `auto` 跟随新主屏
手动检查：M1 无控制台；M2 单实例；M3 分辨率切换生效；M4 拖拽手感；M5 右键菜单；M6 设置热更新；M7 不支持分辨率拦截；M8 自启注册表；M9 重启保留；M10 发布可运行；M11 出屏钳制；M12 删配置默认右上角；M13 主屏切换生效；M14 通知区域图标可显示，左键打开设置，右键打开设置/退出菜单，点击其他应用或桌面后菜单关闭。

**回归规则**：任何用例失败 = Blocker；修 bug 先写复现用例。

### 6.1 UI 迁移的教训（2026-08 WinForms→WPF）

迁移 UI 框架时自动化测试只覆盖了 Core 层，UI 行为（渲染、窗口生命周期、设置交互）全部靠手动，导致 4 个回归未被发现：图标不渲染、设置窗 NRE 闪退、位置钳制误判、透明渲染崩坏。**结论与对策**：

1. Core 层与 UI 的"边界契约"必须有测试：`GetMonitorBounds`/`GetPrimaryDeviceName` 这类被 UI 依赖的 Core API 已纳入（L3）。
2. UI 迁移类改动必须完整跑一遍 M1–M13 手动清单后才算完成，不得只验证"能启动"。
3. 全局异常拦截 + 文件日志（Logger）是 UI 层的最后防线，任何未处理异常必须落盘可查。

## 7. 架构约束

1. 主工程禁止 NuGet 包；tests 仅 xunit + Test.Sdk
2. P/Invoke 只在 `Core/DisplayApi.cs`
3. `Core/` 禁止引用 WPF/WinForms 类型（Screen 枚举等一律走 DisplayApi 的 Win32 封装）
4. 单文件以 ≤ 500 行为目标，超过时尽量按职责拆分
5. 公开 API 变更先更新本文档
6. 可测性：ResolutionSwitcher 走 internal 委托注入；AppConfigStore 带路径重载
7. 中文注释与文案；`WinExe` 无控制台；不创建 .bat
8. 配置只写 `%APPDATA%\ResSwitcher`
9. 失败操作必须保留可诊断错误上下文；UI 必须显示可执行的处理建议和日志文件位置

## 8. 已知实现要点（踩坑记录）

- **P/Invoke**：`[LibraryImport]` 需 unsafe 且不支持含 string 结构体（SYSLIB1051/1062），用经典 `[DllImport(CharSet=Unicode)]`
- **DEVMODEW**：字段顺序是 ABI 契约；`dmPositionX/Y` 紧跟 `dmDisplayFrequency`；`dmSize` 用 `Marshal.SizeOf` 初始化
- **主屏判定**：使用 `MONITORINFOF_PRIMARY` 标志；设主屏时将目标位置提交到虚拟桌面原点 `(0,0)`，不依赖 UI 框架的 Screen 类
- **WPF 悬浮窗**：`AllowsTransparency=true + WindowStyle=None + Background=Transparent`，并在句柄创建后应用 `WS_EX_TOOLWINDOW`、清除 `WS_EX_APPWINDOW`，从任务栏窗口列表和 Alt+Tab 隐藏；边缘干净（WinForms TransparencyKey 方案有抗锯齿杂边问题，已弃用）
- **exe 路径**：单文件发布用 `Environment.ProcessPath`（Assembly.Location 为空）
- **日志**：每个进程会话使用独立日志文件名，写入时清理最后写入时间超过 3 天的旧日志
- **显示 API 兼容性**：`CDS_TEST` 通过只代表驱动接受模式预检；在部分现代显示驱动上，真正使用 `CDS_UPDATEREGISTRY` 应用分辨率或主屏变更仍可能返回 `-1`。当前实现优先使用 Windows Display Configuration API（`QueryDisplayConfig`/`SetDisplayConfig`）提交完整活动拓扑，并正确解码虚拟模式下 source mode 索引的高 16 位；失败时才回退到 legacy API。两条路径均可能因显示驱动或当前会话状态被拒绝
- **WPF/WinForms DPI**：WPF 单位是设备无关像素（1/96"），与 Win32 物理像素换算需 `CompositionTarget.TransformToDevice`
- **ApplicationContext→Application**：WPF 用 `Application` 子类作组合根，`MainWindow` 绑定悬浮窗，关闭即退出
