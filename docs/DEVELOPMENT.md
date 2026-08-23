# SeerNote 开发指南

## 1. 环境

- Windows 11
- Visual Studio 2022 Build Tools
- Windows 11 自带的 .NET Framework 4.8/4.8.1 WPF 程序集
- PowerShell 7（Windows PowerShell 5.1 也应能运行发布产物，不要求用于构建）

`build.ps1` 使用 `vswhere.exe` 定位 Visual Studio Roslyn 编译器，并引用系统 GAC 中的 WPF 程序集；不依赖 `dotnet`、`msbuild` 已加入 PATH 或额外 targeting pack。界面采用代码式 WPF，避免把环境安装步骤变成发布依赖。

当前构建不是严格密封的 .NET Framework 4.8 reference-assembly 构建：编译解析结果取决于构建机已安装的 .NET Framework 4.x GAC（本次验证机为 Windows 11）。发布程序的目标框架仍为 .NET Framework 4.8，但若需要跨机器完全可复现的 API 基线，应另行安装并显式钉住 4.8 reference assemblies。

## 2. 目录与产物

```text
SeerNote/
├─ SeerNote.exe              # Release 复制到根目录
├─ SeerNote.Cli.exe          # 同目录智能体 CLI，引用 SeerNote.exe
├─ fonts/                    # Release 私有字体与 OFL 许可证
├─ SeerNote.ico
├─ build.ps1
├─ assets/fonts/             # 受控字体源文件与许可证
├─ src/SeerNote/
├─ src/SeerNote.Cli/
├─ tests/SeerNote.Tests/
├─ docs/
└─ data/                     # 运行时生成，源码不预置用户内容
```

构建中间产物只进入 `artifacts/`、`src/**/bin` 和 `src/**/obj`；Release 任务最后显式复制根启动文件。

## 3. 命令

```powershell
./build.ps1 -Task Build    # 编译桌面应用与 CLI
./build.ps1 -Task Test     # 编译两个 EXE 并运行确定性测试
./build.ps1 -Task Release  # 更新根目录两个 EXE 与必要资产
./build.ps1 -Task Verify   # Test + Release + CLI 契约与发布结构检查
```

脚本发生错误必须非零退出，并保留 MSBuild 的原始失败上下文。

## 4. 开发顺序

1. 领域模型、搜索和 Note 变量纯函数。
2. 原子文件与 PortableStore，先完成故障路径测试。
3. ViewModel 与主窗口。
4. 热键、托盘、剪贴板、单实例和置顶小窗。
5. Agent/CLI 契约与工作区锁。
6. 图标、根发布、真实交互与视觉验收。

## 5. 测试策略

测试项目是一个无第三方依赖的控制台程序：每个测试独立使用临时目录，失败抛出异常并输出测试名。

必须覆盖：

- 中文、emoji、组合字符和多行文本序列化往返。
- 搜索标题/正文/分类与智能视图排序。
- `{{变量}}` 去重、替换、取消和空变量处理。
- 首次保存、原子替换、备份、主文件损坏恢复。
- 相同 ID 或不支持 schema 的候选被拒绝。
- 回收站还原和永久删除领域行为。
- 单实例第二启动退出。
- CLI schema、创建/检索/更新/软删除/回收站/还原、结构化错误、退出码、stdin 正文与工作区锁冲突。
- `seernote.note.v1` 的小写 UUID、字段集合和 ISO-8601 UTC 时间戳。

UI 必须用真实应用补足：测试程序不替代渲染、IME、DPI、焦点和托盘验证。

## 6. 发布验证

- 版本级留存最多两个：当前版与上一版。完成新版本验收后，删除更早的 `docs/progress/releases/`、对应 version round/phase 和仅用于旧版验收的生成物；长期历史只保留在 `docs/CHANGELOG.md` 摘要中。
- `SeerNote.exe` 与 `SeerNote.Cli.exe` 位于根目录且都嵌入自有图标；CLI 必须能从同目录解析主程序集。
- `fonts/SourceHanSansCN-Regular.otf` 与 `fonts/OFL-SourceHanSans.txt` 位于根目录发布树，哈希与 `assets/fonts/` 受控源一致。
- 应用从私有目录加载字体，无需在 Windows 安装；字体不可用时可回退系统字体并显示状态。
- 无第三方 DLL、无安装器、无包管理器运行时。
- 两个 EXE 分别小于 5 MiB；两个 EXE、必要字体和字体许可证合计小于 10 MiB。
- 发布后的 `SeerNote.Cli.exe schema` 与 `version` 各返回一个可解析的成功 JSON 对象，版本与契约匹配。
- 在全新 `data/` 下启动并创建示例条目。
- 关闭后复制整个目录到另一位置，启动后内容一致。
- 第二实例不会产生第二个数据写入者。
- 程序关闭后没有遗留进程或文件锁。

## 7. 视觉检查

至少保存并查看以下截图：

- 1080p、1200p、2560×1440 和 3840×2160 可用工作区下的首次启动尺寸与三栏重排，包含中文 Note、收藏置顶和自定义分类。
- `860×540` 最小主窗口，长标题和长分类。
- 空状态、无结果、保存失败（可注入）和回收站。
- 默认与回收站编辑区底部的智能体交接组、删除/恢复组及长文本状态。
- 变量填写对话框、右键菜单、永久删除确认和置顶小窗。
- 150% DPI 或等价缩放。

截图是证据，不进入最终便携发布目录。
