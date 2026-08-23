# SeerNote Progress

## Current State

`1.7.0` 已完成智能体友好的桌面 UI 重设计、四档分辨率适配、六组自动化测试、十二种真实 WPF 渲染和根目录便携发布。

## Recent Progress

- 核心 WPF 控件采用统一语义模板；搜索范围、快捷键、本地保存状态、导航数量与分类数量持续可见。
- “新建”降为次级动作，“复制正文”保持活动 Note 唯一实心主操作；收藏金色化，危险操作继续独立分区。
- 紧凑、标准、宽屏和超宽布局已在 minimum、default、1080p、1200p、2K 与 4K 渲染中检查；回收站、空状态和四主题同时覆盖。
- `Ctrl+N`、标题焦点、设置窗口与 UI Automation 文本已做可见窗口检查；自动化焦点缓存不作为产品焦点真相。
- `build.ps1 -Task Verify` 一次通过，根目录双 EXE 已发布为 `1.7.0.0`；根目录 DLL 数与发布后 SeerNote 进程数均为 0。

## Next

本轮发布已完成。后续若新增独立交互域，应从 `MainWindow` 抽取有行为测试保护的展示边界；真实混合 DPI 多显示器拖动与中文 IME 长会话仍可补充，不阻塞当前版本。

## Risks

- 十二种主窗口状态已实际离屏渲染，并完成基础可见窗口交互；尚未在多台真实物理显示器间执行拖动验收。
- 尚未在混合 DPI 多显示器之间手工拖动窗口；Per-Monitor V2 manifest 与逻辑 DIP 策略已静态确认。
- 尚未以真实 junction/SUBST 别名执行双进程手工检查；规范化身份和共享锁已有自动化覆盖。
- 故障测试使用可控文件占用/写入失败，没有注入真实断电或磁盘满。
- 构建从系统 GAC 解析程序集，尚未严格钉住 .NET Framework 4.8 reference assemblies。

## Detailed Record

[1.7.0 智能体友好 UI 重设计](progress/rounds/2026-08-23-round-002-agent-friendly-ui.md)

[1.7.0 发布验收](progress/releases/v1.7.0/ACCEPTANCE.md)

[1.6.1 复制正文下移](progress/rounds/2026-08-23-round-001-copy-body-bottom.md)

[1.6.1 发布验收](progress/releases/v1.6.1/ACCEPTANCE.md)

[1.6.0 智能体 CLI 与桌面交接](progress/rounds/2026-08-22-round-001-agent-cli.md)

[变更记录](CHANGELOG.md)
