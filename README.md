# SeerNote

SeerNote 是一款面向 Windows 11 的中文便携 Note 工具：一处快速记录，一处即时搜索，同时为人和本地智能体提供稳定的交接方式。

## 产品边界

- 所有内容都是统一的 Note；正文含 `{{变量}}` 时，复制前自动询问变量值。
- 左侧支持自定义分类的创建、重命名、删除、拖拽排序，以及把 Note 拖入分类。
- 左侧固定顺序为“收藏置顶、所有条目、自定义分类、回收站”；Note 支持复制、收藏、置顶小窗、移动分类和删除等右键操作。
- 主窗口按紧凑、标准、宽屏和超宽四档自适应 1080p、1200p、2K 与 4K 工作区；导航与分类显示实时数量，搜索范围、快捷键和本地保存状态始终可见。
- 右侧底部集中提供“复制正文、复制 ID、复制为 JSON”，交接操作与删除/恢复保持分区；回收站 Note 仍可复制 ID/JSON 供智能体检查。
- 同目录的 `SeerNote.Cli.exe` 提供 `schema/categories/list/get/create/update/delete/restore` 命令；成功输出单个 JSON 对象，失败输出稳定错误码与结构化 JSON。
- 全局快捷键唤起，输入即搜索。
- 自动保存、本地备份、回收站和崩溃恢复。
- 条目可打开为独立置顶小窗。
- 无账号、无网络、无遥测、无订阅、无安装器。

CLI 用法见 [命令行接口](docs/CLI.md)；交互细节与明确非目标见 [产品规格](docs/PRODUCT.md)；技术结构见 [架构文档](docs/ARCHITECTURE.md)。

## 便携约定

首个便携版已经发布。直接从仓库根目录运行：

```text
SeerNote.exe
SeerNote.Cli.exe schema
```

运行数据位于同目录的 `data/`。关闭程序后复制整个 SeerNote 文件夹，即可完整迁移程序、设置、便签和备份。

`SeerNote.Cli.exe` 必须与 `SeerNote.exe` 保持同目录；两个程序共享该目录下的 `data/notes.json` 和 `.seernote.lock`。桌面应用运行时，CLI 数据命令会返回 `workspace_busy`，避免并发读写或恢复竞争。

发布目录中的 `fonts/` 是桌面应用私有字体资产，必须和两个 EXE 一起复制。SeerNote 会直接从该目录加载思源黑体 CN Regular，不安装或修改 Windows 系统字体；许可证随字体一并保留。

## 发布状态

`1.7.0` 已发布到仓库根目录，完成智能体友好的桌面工作台重设计、四档分辨率适配、四主题与关键状态真实 WPF 渲染、六组自动化测试及便携结构验收。权威状态见 [项目进度](docs/PROGRESS.md)，发布证据见 [1.7.0 发布验收](docs/progress/releases/v1.7.0/ACCEPTANCE.md)。

## 构建

本机需安装 Visual Studio 2022 Build Tools；构建脚本使用其中的 Roslyn 编译器，并从当前 Windows 的系统 GAC 解析 .NET Framework WPF 程序集：

```powershell
./build.ps1 -Task Verify
```

详细命令和产物规则见 [开发指南](docs/DEVELOPMENT.md)。

## 参与项目

提交改动前请阅读 [贡献指南](CONTRIBUTING.md)。安全问题请按 [安全政策](SECURITY.md) 私下报告，不要在公开 Issue 中附带敏感 Note、路径或备份内容。

## License

SeerNote 源码按 [MIT License](LICENSE) 发布；随包的 Adobe 思源黑体保持其独立的 SIL Open Font License 1.1，许可证见 `fonts/OFL-SourceHanSans.txt`。第三方参考项目仅借鉴公开交互与工程原则，没有复制其代码或品牌资产。
