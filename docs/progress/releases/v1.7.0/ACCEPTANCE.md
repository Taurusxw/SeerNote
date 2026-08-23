# SeerNote 1.7.0 发布验收

验收日期：2026-08-23

结论：UI 实现、自动化、真实 WPF 渲染、可见窗口交互与根目录便携发布全部通过。

## 自动化行为

`build.ps1 -Task Verify` 通过六组测试：

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

- 核心控件模板保留原生文本内容宿主，并在悬停、焦点、选中状态下保持几何稳定。
- Primary、Quiet、Toolbar、Navigation 与 Danger 角色都来自统一主题边界。
- 四档布局列宽、编辑区最低尺寸、唯一主操作、保存状态、分类数量和回收站边界均有确定性断言。

## 真实 WPF 视觉证据

已生成并检查十二张 PNG：

```text
minimum          860×506
default         1080×686
1080p           1304×742
1200p           1304×830
2K              1736×1014
4K              1920×1246
deleted         1080×686
empty           1080×686
Graphite        1080×686
Midnight        1080×686
Porcelain       1080×686
Sage            1080×686
```

搜索、标题/保存状态、三栏层级、数量、底部交接和危险动作均无裁切；长分类不产生横向滚动。四主题共享相同布局和可见焦点。

## 可见窗口交互

- UI Automation 名称和说明可读取。
- `Ctrl+N` 创建 Note，标题可见焦点和本地保存状态正确。
- 设置窗口与两个折叠组使用一致控件语法。
- 自动化辅助程序对 WPF owner/modal 的焦点报告存在缓存与目标窗口限制；以可见焦点、原生 `IsCancel` 语义和确定性控件检查作为发布证据。

## 根目录发布与结构

- `build.ps1 -Task Verify` 一次通过，输出 `PUBLISH_STRUCTURE_OK` 与 `VERIFY_OK`。
- 根目录 `SeerNote.exe` 为 194,048 bytes，`SeerNote.Cli.exe` 为 19,456 bytes，文件版本均为 `1.7.0.0`；必要便携分发为 8,647,191 bytes。
- `SeerNote.Cli.exe version` 返回 `seernote.cli.v1`、版本 `1.7.0` 与退出码 0。
- 根目录 DLL 数为 0；图标、私有字体、许可证与尺寸预算由发布脚本验证。
- 发布后 SeerNote 进程数为 0，没有遗留桌面实例。
- 发布记录只保留当前 `1.7.0` 与上一版 `1.6.1`；原始历史 round 与完整 `CHANGELOG.md` 保留追溯证据。
