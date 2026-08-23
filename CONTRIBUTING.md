# Contributing to SeerNote

感谢你帮助改进 SeerNote。项目优先保持 Windows 11 本地、便携、中文优先、零账号/网络/遥测和零第三方运行时依赖。

## 开始之前

- 阅读 `AGENTS.md`、`README.md` 与受影响的产品/架构文档。
- 使用 Visual Studio 2022 Build Tools 和 Windows 自带的 .NET Framework 4.8/4.8.1。
- 不提交 `data/`、`artifacts/`、`bin/`、`obj/`、截图、备份或本机路径缓存。

## 提交改动

1. 保持改动小而完整，复用现有 Domain、Storage、Platform、Presentation 与 Theme 边界。
2. UI 文案使用简体中文，标准 WPF 文本控件必须保留 IME、键盘和 UI Automation 行为。
3. 数据删除默认进入回收站；不得绕过原子保存、备份、恢复或工作区锁。
4. 只通过 `./build.ps1` 构建和测试。提交前至少运行与改动相称的任务；发布候选运行一次 `./build.ps1 -Task Verify`。
5. 用户可见变化更新 `docs/PRODUCT.md` 与 `docs/CHANGELOG.md`；边界变化更新 `docs/ARCHITECTURE.md`。

请在 Pull Request 中说明行为变化、验证结果和仍未覆盖的风险。不要附带真实 Note、秘密、个人路径或便携数据目录。
