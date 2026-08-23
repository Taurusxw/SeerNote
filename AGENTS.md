# SeerNote Project Rules

本项目同时遵守当前任务加载的全局 Codex 规则。本文件只记录 SeerNote 的项目事实与长期约束。

## Project Goal

SeerNote 是 Windows 11 上的本地、便携、中文优先的随手便签与提示词工具。成功意味着：解压后直接运行、数据随目录迁移、启动与搜索迅速、无账号/网络/遥测、界面克制且所有可见操作真实可用。

## Tech Stack

- C# / WPF，目标框架 `.NET Framework 4.8`。
- `AnyCPU`，不依赖 NuGet、WebView、Electron、数据库服务或安装器。
- 本地权威数据：`data/notes.json`；原子替换、自动备份和启动恢复。
- Windows 平台能力集中在 `Platform/`：全局热键、托盘、剪贴板、单实例。

## Directory Map

- `SeerNote.exe`：根目录可直接启动的发布产物。
- `data/`：运行时数据和备份；不得纳入源代码提交。
- `src/SeerNote/`：应用源码。
- `tests/SeerNote.Tests/`：无第三方测试框架的确定性测试程序。
- `assets/`：图标源文件和发布图标。
- `docs/`：产品、架构、开发、参考资料、ADR 与阶段记录。
- `build.ps1`：唯一支持的构建/测试/发布入口。

## Common Commands

```powershell
./build.ps1 -Task Build
./build.ps1 -Task Test
./build.ps1 -Task Release
./build.ps1 -Task Verify
```

`Release` 必须把最终 `SeerNote.exe` 和必要资产复制到仓库根目录；不得要求用户从 `bin/` 中寻找程序。

## Task Level Defaults

- 普通 UI、搜索或存储修复按 L2。
- 数据格式、原子保存、恢复、全局热键或发布结构变化至少按 L3。
- 发布和兼容性验收按 L4 阶段清单执行一次。

## Documentation Mode

- 用户行为变化：更新 `docs/PRODUCT.md` 和 `docs/CHANGELOG.md`。
- 模块、存储或平台接口变化：更新 `docs/ARCHITECTURE.md`；难逆决策才新增 ADR。
- 构建与验证变化：更新 `docs/DEVELOPMENT.md` 和当前 phase。
- `docs/PROGRESS.md` 只保留当前状态与入口，细节放 phase。

## Code Conventions

- UI 文案全部使用简体中文；类型、成员和文件名使用清晰英文。
- 优先标准 WPF 控件，保留中文 IME、键盘、UI Automation 和 DPI 行为。
- 模块以小接口隐藏实现；没有第二个真实 Adapter 时不创建假接口。
- 文件系统、序列化和备份不得在 UI 线程执行长循环。
- 不捕获并静默吞掉异常；写入失败必须保留内存草稿并给出恢复动作。
- 不添加网络、更新、遥测、账号、插件或脚本执行能力，除非用户明确扩展范围。

## Test And Completion Standard

- 领域/搜索/模板变量/存储恢复测试通过。
- Release 构建通过，根目录产物可启动。
- 实际渲染并检查默认尺寸、最小尺寸、中文长文本、空状态、错误状态和置顶小窗。
- 实际操作新建、自动保存、重启恢复、搜索、复制模板、删除/还原、全局唤起和单实例。
- 验证关闭后复制整个目录仍可读取原数据；程序不得在注册表写入产品设置。
- 当前 phase 的固定验收项通过后立即收尾，不把可选增强并入当前目标。

## Git And Collaboration Boundaries

- 多 Agent 并行时，每个生产文件只能有一个写入负责人；根 Agent 负责跨模块集成。
- 保留用户与其他 Agent 的已有修改，不回滚、不批量格式化无关文件。
- 不提交 `bin/`、`obj/`、运行时 `data/`、临时截图或本机路径缓存。

## Risk Boundaries

- 删除默认进入回收站；永久删除必须明确显示对象并确认。
- 原子保存、备份和恢复逻辑属于数据完整性边界，必须有失败路径测试。
- 不保存密码、API Key 或其他秘密；应用不提供加密承诺。
- 不自动修改开机启动、文件关联或注册表，保持可移除性。
