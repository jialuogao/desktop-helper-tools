# ResSwitcher

[English](README.md) | 简体中文

一个轻量的 Windows 悬浮工具，用一个可拖拽的按钮快速切换显示器分辨率，也可以在多显示器之间切换主屏。

它适合需要频繁在高分辨率与低分辨率之间切换的场景，例如游戏、远程桌面、演示或串流。程序常驻后台，不占用任务栏窗口列表和 Alt+Tab，并在 Windows 通知区域保留入口。

## 功能

- 双区域悬浮按钮：左侧切换主屏，右侧切换分辨率
- `auto` 分辨率目标跟随当前主屏；主屏互换后可继续操作新的主屏
- 每块显示器独立保存分辨率列表，自动过滤当前显示器不支持的模式

- 支持单项往返和多项循环两种分辨率切换方式
- 使用 Windows Display Configuration API（CCD）提交完整显示拓扑，并保留传统 API 作为兼容回退
- 拖拽定位、位置记忆、悬停显现和可调透明度

- 支持开机自启、单实例运行和 JSON 配置
- 通知区域图标可快速打开设置或退出程序
- 基于 WPF 和 .NET 10，主项目没有第三方运行时依赖

## 快速开始

### 使用已发布程序

运行仓库中的发布脚本后，会在 `dist\ResSwitcher.exe` 生成发布产物。双击运行后，悬浮按钮会出现在当前主屏右上角，Windows 通知区域也会出现 ResSwitcher 图标。

框架依赖版本需要安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。如果没有运行时，可以按下面的发布命令生成自包含版本。

### 基本操作

1. 左键拖动按钮到合适的位置。
2. 点击左半区，在当前主屏和另一块活动显示器之间互换主屏。
3. 点击右半区，按设置中的顺序切换分辨率。
4. 右键悬浮按钮或通知区域图标打开「设置…」或「退出」；左键通知区域图标打开设置。
5. 在设置中选择「自动（当前主显示器）」或固定显示器，并为每块显示器添加支持的分辨率。

设置保存后立即生效。配置文件位于 `%APPDATA%\ResSwitcher\config.json`；删除该文件可以恢复默认配置。

## 构建与测试

开发和发布需要 .NET 10 SDK，可用 `dotnet --list-sdks` 检查。

```powershell
# 构建
dotnet build -c Release

# 运行全部测试
dotnet test tests/ResSwitcher.Tests -c Release

# 构建、测试并发布 win-x64 单文件程序
.\build-release.ps1
```

发布脚本会将产物写入 `dist\ResSwitcher.exe`。也可以手动发布：

```powershell
dotnet publish src/ResSwitcher -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:SelfContained=false `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

需要在没有 .NET 10 Desktop Runtime 的机器上运行时，将 `-p:SelfContained=false` 改为 `-p:SelfContained=true`，生成的文件体积会更大。

当前自动化测试共 38 个，覆盖切换状态机、显示器 profile、配置读写、日志、自启、CCD 索引和主屏切换目标选择。真实显示驱动提交仍需在目标机器上进行手动验收。

## 技术概览

```text
src/ResSwitcher/
├── Core/                 显示 API、切换状态机、配置、日志和自启
├── Ui/                   WPF 悬浮窗与设置窗口
├── AppContext.cs         应用组合根
└── Program.cs            单实例入口

tests/ResSwitcher.Tests/  离线单元测试和 API 边界测试
doc/                      行为规格与实现笔记
```

所有 Win32 P/Invoke 集中在 `Core/DisplayApi.cs`。显示配置使用 `QueryDisplayConfig` / `SetDisplayConfig`，分辨率切换前会读取目标显示器的实时支持列表。详细行为规格见 [`doc/PROJECT.md`](doc/PROJECT.md)，实现笔记索引见 [`doc/ai-implementation-notes.md`](doc/ai-implementation-notes.md)。

## 已知限制

- 分辨率和主屏提交受 Windows 会话、显卡驱动、扩展/复制模式以及全屏程序影响；系统拒绝请求时程序会显示错误上下文并写入会话日志。
- 主屏切换会改变 Windows 虚拟桌面坐标。程序会同步调整悬浮按钮位置，但不同 DPI 缩放组合仍建议在目标设备上检查一次。
- 当前发布脚本默认生成框架依赖的 `win-x64` 单文件版本。

## 参与贡献

欢迎提交 Issue 或 Pull Request。行为改动请同时补充对应测试，并在提交前运行：

```powershell
dotnet build -c Release
dotnet test tests/ResSwitcher.Tests -c Release
.\build-release.ps1
```

行为规格、手动验收项和架构约束集中在 [`doc/PROJECT.md`](doc/PROJECT.md)。

## 许可证

本项目以 MIT License 发布，详见 [`LICENSE`](LICENSE)。
# ResSwitcher — 屏幕分辨率切换悬浮工具

Windows 常驻后台小工具：屏幕上有一个可拖拽的半透明圆角长方形悬浮按钮，单击一键切换指定显示器分辨率（如 3440x1440 ↔ 2560x1440），右键打开设置或退出。支持开机自启。

## 功能简介

- **悬浮按钮**：左区切换主显示器，右区切换分辨率；可拖拽，拖拽时完全不透明，松手后约 600ms 淡出至静止透明度（默认 0.35）
- **悬停显现**：鼠标移到按钮上时立即完全显现，移开后恢复静止透明度
- **单击切换**：
  - 单分辨率模式：在"当前"和"设定"分辨率之间来回切换
  - 双分辨率模式：固定在两个分辨率之间切换；若当前是其他分辨率，第一次点击切到分辨率1，再点切到分辨率2，不回到原始
- **右键菜单**：设置… / 退出
- **设置界面**：开机自启、自动或固定目标显示器、按钮大小/颜色/静止透明度；每块显示器独立配置分辨率列表，不支持的项目自动灰显
- **位置记忆**：按钮位置自动保存，下次启动恢复；首次运行出现在屏幕 1 右上角
- **主屏互换**：切换主屏时保持显示器相对布局，并保持悬浮按钮在原物理位置
- **单实例**：重复启动自动退出
- 零第三方运行时依赖（.NET 10 + WPF）

## 使用方法

1. 运行 `ResSwitcher.exe`，屏幕右上角出现半透明圆角长方形按钮
2. 拖动到任意位置；单击切换分辨率
3. 右键 →「设置…」配置显示器、外观、分辨率模式；勾选「开机自动启动」可注册自启
4. 右键 →「退出」结束程序

配置文件位于 `%APPDATA%\ResSwitcher\config.json`，可手动编辑或删除后重置。

项目说明：行为规格见 [`doc/PROJECT.md`](doc/PROJECT.md)，面向 agent 的实现索引见 [`doc/ai-implementation-notes.md`](doc/ai-implementation-notes.md)。

## 构建方法

需要 .NET 10 SDK（`dotnet --list-sdks` 确认）。

```powershell
# 构建
dotnet build -c Release

# 运行测试
dotnet test tests/ResSwitcher.Tests -c Release

# 发布单个 exe（本机需已装 .NET 10 运行时）
dotnet publish src/ResSwitcher -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:SelfContained=false `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

产物：`src/ResSwitcher/bin/Release/net10.0-windows/win-x64/publish/ResSwitcher.exe`

> 如需分发到没有 .NET 运行时的机器，把 `-p:SelfContained=true` 即可（体积会变大）。
