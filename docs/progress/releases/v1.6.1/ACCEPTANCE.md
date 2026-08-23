# SeerNote 1.6.1 发布验收

验收日期：2026-08-23

结论：UI 实现、自动化、离屏渲染与根目录便携发布全部通过。

## 自动化行为

`build.ps1 -Task Test` 通过六组测试：

```text
PASS DomainTests
PASS StorageTests
PASS ThemeTests
PASS ApplicationTests
PASS CliTests
PASS PresentationTests
ALL_TESTS_PASSED
```

直接覆盖本版目标：

- 顶部命令栏不再包含“复制正文”，仍包含收藏与置顶小窗。
- 底部交接组严格按“复制正文、复制 Note ID、复制为 JSON”排序，且只有一个正文复制按钮。
- 回收站状态隐藏正文复制，保留 ID/JSON、还原和永久删除。
- 既有 `Ctrl+Enter` 和右键菜单复制路径保持回归覆盖。

## 离屏视觉证据

真实 WPF PNG 已生成并检查：

```text
minimum:  860×506
default: 1080×686
2K:     1736×1014
deleted: 1080×686
```

三种复制操作集中于底部左侧并保持一行；删除/恢复在右侧保持语义隔离。最小窗口正文可达，回收站没有出现无效的正文复制按钮。

## 根目录发布与结构

- `build.ps1 -Task Verify` 一次通过，输出 `PUBLISH_STRUCTURE_OK` 与 `VERIFY_OK`。
- 根目录 `SeerNote.exe` 为 186,368 bytes，`SeerNote.Cli.exe` 为 19,456 bytes，文件版本均为 `1.6.1.0`；必要便携分发为 8,639,511 bytes。
- `SeerNote.Cli.exe version` 返回 `seernote.cli.v1`、版本 `1.6.1` 与退出码 0。
- 根目录 DLL 数为 0；图标、私有字体、许可证与尺寸预算由发布脚本验证。
- 发布后 SeerNote 进程数为 0，没有遗留桌面实例。
- 发布记录只保留当前 `1.6.1` 与上一版 `1.6.0`；更早版本的长期变化继续保留在 `CHANGELOG.md`，原始历史 round 保留作追溯证据。
