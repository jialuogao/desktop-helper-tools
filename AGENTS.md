# AGENTS.md — ResSwitcher 开发约束

> 本文件约束所有 AI 代理与人类开发者在仓库中的行为。**开始任何工作前必须完整阅读本文件。**

## 项目速览

- Windows 悬浮按钮分辨率切换工具。C# / .NET 10 / WPF，零第三方运行时依赖。
- 行为规格与项目约束：`doc/PROJECT.md`；agent-facing 实现索引：`doc/ai-implementation-notes.md`；使用者文档：`README.md`。

## 开发-测试循环（强制流程）

每次代码改动（无论大小）必须走完以下循环，禁止跳步：

```
1. 改动前：读 doc/PROJECT.md 对应章节，明确本次改动范围
2. 编码：遵守下方"编码规则"
3. 构建：dotnet build -c Release        → 必须 0 error 0 warning
4. 测试：dotnet test tests/ResSwitcher.Tests -c Release
   → 全部通过（28 个用例）
5. 自检：对照 doc/PROJECT.md §7 架构约束逐条核对
6. 若改动涉及行为（非纯重构）：跑 PROJECT.md §6 手动检查项中受影响的 M 项
7. 发布验证：.\build-release.ps1（构建→测试→发布 dist\ResSwitcher.exe）
8. 汇报：说明改了什么、构建/测试结果、约束自检结论
```

**回归判定**：任何自动化用例失败 = Blocker，禁止继续新功能开发，先修复。

## 编码规则

1. 目标框架 `net10.0-windows`；主工程禁止新增 NuGet 包（tests 工程仅允许 xunit + Test.Sdk）。
2. 所有 P/Invoke 只允许在 `Core/DisplayApi.cs`。
3. `Core/` 下禁止引用 WinForms 控件类型；UI 逻辑只在 `Ui/`。
4. 单文件以 ≤ 500 行为目标；超过时尽量按职责拆分（参考现有 Core/Ui 分层）。
5. UI 文案与注释使用中文。
6. 不创建 `.bat` / 控制台输出启动脚本（build-release.ps1 等构建脚本除外）；`OutputType=WinExe`。
7. 公开 API 签名变更须先更新 `doc/PROJECT.md` 再改代码。
8. 可测性：`ResolutionSwitcher` 对 DisplayApi 的访问必须走可注入委托；`AppConfigStore` 必须提供带路径重载。
9. agent-facing 实现细节写入 `doc/impl-notes/`，不要在 `doc/ai-implementation-notes.md` 中堆积具体实现正文。

## 测试规则

1. 新增/修改 `Core/` 行为时，必须同步新增或更新 `tests/ResSwitcher.Tests/` 对应用例，并在提交说明中列出用例 ID（如 D1–D6）。
2. 修复 bug 时：先写一个能复现该 bug 的失败用例，再修复使其通过。
3. 禁止删除或跳过（`[Skip]`）现有用例来让测试通过；确需变更用例须在汇报中说明理由。

## Git 约定

- 提交信息格式：`<feat|fix|test|doc>: <中文简述>`，如 `fix: 双模式在 res1==res2 时误调用切换 API`。
- 行为改动与对应测试放在同一提交，便于回溯回归点。

## 禁止事项

- 禁止引入第三方 UI 框架（WPF/MAUI/Avalonia）或改写技术选型。
- 禁止让程序弹出到任务栏（悬浮窗 `ShowInTaskbar=false` 不得更改）。
- 禁止在 Toggle 失败时静默吞掉而不返回 `SwitchResult`。
- 禁止把配置写到安装目录（必须 `%APPDATA%\ResSwitcher`）。
