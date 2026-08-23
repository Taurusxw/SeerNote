# SeerNote 1.8.0 发布验收

验收日期：2026-08-24

结论：通过。当前源码、根目录便携程序和发布资产满足 `v1.8.0` 提交、标签与 GitHub 公开发布条件。

## 版本与范围

- 桌面端和 CLI 的程序集/文件版本均为 `1.8.0.0`；`SeerNote.Cli.exe version` 返回 `1.8.0`。
- 本版纳入 2026-08-23 至 2026-08-24 已完成的搜索可达性、UIA 状态反馈和 Phase 001 大集合响应性改进，不改变数据 schema 或 CLI 契约。
- 既有交接、项目规则、进度和 `v1.7.0` 验收快照未在本轮重读、重写或复验；本验收使用全新的 `v1.8.0` 发行记录。

## 自动化与发布结构

最终源码上一次执行 `build.ps1 -Task Verify`，通过全部六组测试并更新根目录便携程序：

```text
PASS DomainTests
PASS StorageTests
PASS ThemeTests
PASS ApplicationTests
PASS CliTests
PASS PresentationTests
ALL_TESTS_PASSED
Published: SeerNote.exe (210944 bytes), SeerNote.Cli.exe (19456 bytes); portable distribution 8664087 bytes
PUBLISH_STRUCTURE_OK
VERIFY_OK
```

该检查同时确认：

- 根目录双 EXE、图标、私有字体与 OFL 许可证齐全；字体与受控源一致。
- 根发布目录不存在第三方 DLL，双 EXE 和完整便携分发均低于既定体积上限。
- 发布后的 CLI `schema` 返回 `seernote.cli.v1` / `seernote.note.v1`，`version` 返回 `1.8.0`。
- 源码未引用超出产品边界的注册表或网络 API。

## 行为与回归覆盖

- 搜索清空、`Enter`/`Esc` 焦点闭环、live-region 优先级和自动保存静默门控有确定性测试与真实 WPF 证据。
- Note/分类虚拟化覆盖完整数据源、容器回收后的 UIA、Tooltip、菜单、选择与拖放目标。
- 筛选、导航与分类选择器缓存覆盖所有当前失效路径；纯选择和文字编辑继续使用窄通知/刷新边界。
- 直接与共享 Note 菜单覆盖打开后选择变化、6→7→6 阈值、移动/收藏/删除/还原及目标引用释放。
- `ClearTrash` 覆盖原列表身份、存活/null 顺序、精确删除数、同步状态观察者、缓存失效与空操作零事件；九格真实调用基准的耗时和分配均为正向收益。

## 隔离便携验收

在 `artifacts/release/v1.8.0/` 下构造不含运行时数据的干净便携目录并验证：

- ZIP 恰好包含双 EXE、`SeerNote.ico`、`fonts/` 两个必要文件、`LICENSE` 和 `README.md`，不包含 `data/`。
- 在第一份隔离副本中用 CLI 创建中文 Note，再复制整个便携目录到第二位置；第二份 CLI 按 ID 读回相同标题和正文。
- 验收结束后桌面端和 CLI 进程数均为 0，没有遗留发布进程。
- 真实项目 `data/` 未被读取、改写、迁移或清理。

## 公开源码与许可审计

- GitHub 仓库当前为 `PUBLIC`，默认分支为 `main`，GitHub 已识别根 `LICENSE` 为 MIT。
- README、MIT、字体 OFL、SECURITY、CONTRIBUTING 与“无账号/网络/遥测”的产品边界一致。
- 公开树与可达 Git 历史未发现凭据、私钥、连接串、用户主目录、数据库、Office/PDF/归档或异常大对象；命中 `token` / `host` 的位置均为代码变量或 UI 容器。
- `.gitignore` 排除真实 `data/`、构建产物、环境文件、密钥证书、数据库、日志和本地缓存。
- 既有 Git 作者邮箱已经随当前公开仓库历史可见，本版不改写历史。

## 发行资产

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| `SeerNote-portable-v1.8.0.zip` | 7474746 | `4d6f755c7600da582ec540be260893920f1ae0c049a53a6ce4bf2b4f1f079564` |
| `SeerNote.exe` | 210944 | `a13999af85f0e5877dacec12fb1aa1b913750fc7803b357ac201ffecb404f3e1` |
| `SeerNote.Cli.exe` | 19456 | `089125530979e5c706f2d439b22eb36e499d1c0843f98ce03a101b2e284f598e` |

`SHA256SUMS.txt` 与上述三个资产一起发布。双 EXE 当前为 `NotSigned`；README 和发布说明已明确 Windows SmartScreen 风险与校验方式，不宣称代码签名或来源证明。

## 清理与保留

- 新版验收完成后，版本级文档只保留当前 `v1.8.0` 与上一版 `v1.7.0`；移除更早的 `v1.6.1` 发行目录，长期变化继续保留在 Changelog 和 Git 历史。
- GitHub 资产上传并验证后，使用项目自带 `build.ps1 -Task Clean` 清除整个 ignored `artifacts/` 中间树，包括旧基准二进制与隔离测试数据；长期结论留在版本文档和 GitHub 资产中。根目录最新程序、Git 历史、`v1.7.0` 标签/Release 与真实 `data/` 保留。

## 已知非阻断风险

- 程序未使用商业代码签名证书，首次下载可能触发 SmartScreen；SHA-256 只能验证与本次发布资产的字节一致性，不能替代签名来源证明。
- 实际 Narrator 语音顺序、物理鼠标/Shift+F10 菜单放置、混合 DPI 多显示器和长时间中文 IME 仍属于人工设备验收项；自动化覆盖其状态、目标和布局边界。
- 性能数值只代表同机基准；保留的是所有测量场景方向一致及复杂度下降的结论。
