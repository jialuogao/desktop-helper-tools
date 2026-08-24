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
