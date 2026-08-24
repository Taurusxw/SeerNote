# SeerNote 1.9.0 发布说明

发布日期：2026-08-24

目标平台：Windows 11 / .NET Framework 4.8

## 用户可见变化

- Note 结果列表支持纵向拖放手工排序，并提供 `Alt+↑/↓` 键盘替代、主题色插入线和长列表边缘自动滚动。
- 收藏仍是固定置顶分组；收藏与普通 Note 只能在各自组内排序，回收站使用独立顺序。
- 搜索或分类筛选状态下只重排当前可见 Note 占据的槽位，隐藏 Note 保持原位置和相对顺序；选择和原有 Note→分类拖放同时保留。
- 7 条以上 Note 使用共享右键菜单时不再退回系统浅灰样式，根菜单、子菜单、分隔线、勾选和键盘高亮继续遵循当前主题。

## 兼容与升级

- 便携数据升级为 `schemaVersion: 3`，`entries` 数组顺序成为权威手工顺序。
- 首次读取 schema 1/2 时，应用先按旧版收藏、更新时间和删除时间规则冻结原显示顺序，再迁移到 schema 3；升级后首次显示不会跳序。
- 桌面端与 CLI 共用新建、收藏切换、软删除和还原的目标组顶部规则；CLI `seernote.cli.v1` / `seernote.note.v1` 契约保持不变。
- 升级前先正常退出 SeerNote 并备份便携目录；用新 ZIP 中的程序和 `fonts/` 覆盖应用文件，保留原 `data/` 目录。
- 1.9 保存为 schema 3 后，1.8 不能直接读取新主文件；降级时必须恢复升级前的完整 `data/` 备份或有效的 schema 2 `notes.json.bak`，不能只替换 EXE。隔离验收确认 1.8 可从该备份恢复旧内容，并把 schema 3 主文件保存在 `data/recovery/`。

## 隐私与分发

- 无账号、网络、遥测、安装器、NuGet 或第三方运行时 DLL；源码继续采用 MIT，思源黑体继续采用 SIL Open Font License 1.1。
- 首选资产是 `SeerNote-portable-v1.9.0.zip`，包含桌面端、CLI、图标、私有字体、字体许可证、MIT 许可证和 README。
- 发布页同时提供双 EXE 与 `SHA256SUMS.txt`。当前程序未使用商业代码签名证书，Windows SmartScreen 可能显示未知发布者；请从项目官方 Release 下载并校验 SHA-256。

完整构建、迁移、行为、便携数据与发行资产证据见 [ACCEPTANCE.md](ACCEPTANCE.md)。
