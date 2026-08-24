# SeerNote 1.9.0 发布验收

验收日期：2026-08-24

结论：通过。当前源码、根目录便携程序和本地发行资产满足 `v1.9.0` 提交、标签与 GitHub 公开发布条件。

## 版本与范围

- 桌面端和 CLI 的程序集/文件版本均为 `1.9.0.0`；`SeerNote.Cli.exe version` 返回 `1.9.0`。
- 本版交付 Note 同收藏组手工排序、过滤视图隐藏槽位保护、`Alt+↑/↓` 键盘路径、schema 3 顺序持久化与共享 Note 右键菜单主题修复。
- CLI `seernote.cli.v1` / `seernote.note.v1` 外部契约保持不变；数据 schema 从 2 升为 3。
- 冻结的 `docs/HANDOFF.md`、真实 `data/`、既有外部进程与非发布删除权限未纳入写集。

## 自动化与发布结构

最终源码执行一次 `build.ps1 -Task Verify`，通过全部六组测试并更新根目录便携程序：

```text
PASS DomainTests
PASS StorageTests
PASS ThemeTests
PASS ApplicationTests
PASS CliTests
PASS PresentationTests
ALL_TESTS_PASSED
Published: SeerNote.exe (217088 bytes), SeerNote.Cli.exe (19456 bytes); portable distribution 8670231 bytes
PUBLISH_STRUCTURE_OK
VERIFY_OK
```

该检查同时确认：

- 根目录双 EXE、图标、私有字体与 OFL 许可证齐全；字体与受控源一致。
- 根发布目录不存在第三方 DLL，双 EXE 和完整便携分发均低于既定体积上限。
- 发布后的 CLI `schema` 返回 `seernote.cli.v1` / `seernote.note.v1`，`version` 返回 `1.9.0`。
- 源码未引用超出产品边界的注册表或网络 API。

## 行为、迁移与回滚覆盖

- 真实 WPF 路由测试覆盖同组拖放效果、主题插入线、实际顺序、选择保持与跨收藏组拒绝；列表辅助技术说明同时公开 `Alt+↑/↓`。
- 领域与应用测试覆盖收藏固定置顶、回收站手工顺序、搜索/分类过滤下可见槽位替换、隐藏槽位稳定、缓存失效与保存后读回。
- CLI 测试覆盖创建、收藏变化、软删除和还原进入目标组顶部；原有 Note→分类拖放测试继续通过。
- schema 1/2 加载会先冻结旧版时间顺序再迁移；schema 3 把 `entries` 数组作为权威手工顺序并完整往返。
- 独立使用官方 v1.8 双 EXE 建立 schema 2 数据后，v1.9 更新产生 schema 3 主文件与 schema 2 `notes.json.bak`；换回 v1.8 时成功从备份恢复旧内容，并把不可读的 schema 3 主文件保存到 `data/recovery/`。因此降级必须恢复备份，不能只换 EXE。

## 隔离便携验收

在 `artifacts/release/v1.9.0/` 下构造不含运行时数据的干净便携目录并验证：

- ZIP 恰好包含双 EXE、`SeerNote.ico`、`fonts/` 两个必要文件、`LICENSE` 和 `README.md`，不包含 `data/`。
- 在第一份隔离副本中用 CLI 创建中文 Note，再复制整个便携目录到第二位置；第二份 CLI 按 ID 读回相同标题和正文。
- 打包源目录始终没有 `data/`；测试数据只存在于 ignored 的发布验收副本。
- 验收结束后桌面端和 CLI 进程数均为 0，没有遗留发布进程。
- 真实项目 `data/` 未被读取、改写、迁移或清理。

## 公开源码与许可审计

- GitHub 仓库为 `PUBLIC`，默认分支为 `main`，GitHub 已识别根 `LICENSE` 为 MIT。
- README、MIT、字体 OFL、SECURITY、CONTRIBUTING 与“无账号/网络/遥测”的产品边界一致。
- 强凭据模式未命中；普通 `token` 命中是主题语义或代码变量，`sk-` 命中来自 “Risk-Mapped” 标题，不是密钥。
- 当前树与历史未发现用户主目录、凭据、私钥、数据库、Office/PDF/归档或异常未知大对象；唯一超过 5 MiB 的受控对象是带 OFL 许可证的思源黑体字体。
- `.gitignore` 排除真实 `data/`、构建产物、环境文件、密钥证书、数据库、日志和本地缓存。
- 既有 Git 作者邮箱和项目级 `E:\Codex\SeerNote` 交接路径已随公开历史可见；本版不改写历史，也不提交冻结交接的当前改动。

## 发行资产

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| `SeerNote-portable-v1.9.0.zip` | 7477115 | `6dcb58a57398a80613daf2836cc0a3a0de81213d786a488bb8deecab3dca4e57` |
| `SeerNote.exe` | 217088 | `928a174efc2b119a91108b43ddd858302305d4264613559c0f64ff3959149a75` |
| `SeerNote.Cli.exe` | 19456 | `e356c800d44a9068dbd04b8585e3095801ef2f1812824f4369af3db9322de447` |

`SHA256SUMS.txt` 与上述三个资产一起发布。双 EXE 为 `NotSigned`；README 和发布说明已明确 Windows SmartScreen 风险与校验方式，不宣称代码签名或来源证明。

## 清理与保留

- 版本级文档只保留当前 `v1.9.0` 与上一版 `v1.8.0`；`v1.7.0` 的版本级文档从当前树移除，长期变化、原始 round、Git 标签和既有 GitHub Release 保留。
- 本地 ignored `artifacts/` 保留本次隔离验收与上传资产，不进入 Git；真实 `data/` 和冻结交接保持原样。

## 已知非阻断风险

- 程序未使用商业代码签名证书，首次下载可能触发 SmartScreen；SHA-256 只能验证与本次发布资产的字节一致性，不能替代签名来源证明。
- 长列表边缘自动滚动有实现与路由测试，但持续物理鼠标拖动、实际 Narrator 语音顺序、混合 DPI 多显示器和长时间中文 IME 仍属于人工设备验收项。
- 降级会恢复 schema 2 备份中的升级前内容，不包含升级后新增或修改；用户必须优先保留升级前完整便携目录备份。
