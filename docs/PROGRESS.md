# SeerNote Progress

## Current State

`1.7.0` 已完成并公开发布；当前未发布工作树继续优化搜索键盘路径、辅助技术状态反馈与大集合列表/筛选性能，不改变数据、CLI 或发布结构。

## Recent Progress

- 编辑器分类下拉在有序分类未变时不再清空并重加全部 WPF 项，只更新当前 Note 的选择；5000 条、100 分类的两次反向交错复跑中，稳定刷新快逾 `99.9%`，真实 Note 切换快 `83.0–85.6%`，搜索快 `18.0–26.2%`，真实分类重排仍快 `2.6–4.4%`。
- 导航快照改由 `MainViewModel` 按影响范围缓存：正文/标题、置顶小窗内容、搜索和纯选择复用，收藏、删除/恢复、移动与分类变化立即失效；5000 条、100 分类的两次交错复跑中，稳定刷新降至 `0.0001 ms`，视图切换快 `66.3–69.0%`，搜索切换快 `56.7–60.7%`，真实收藏变化仍快 `5.6–10.7%`。
- 导航统计从四次集合扫描抽取为单遍只读快照，并跳过内容等价时的控件更新；5000 条、100 分类的稳定刷新约快 `26.9%`，真实计数变化仍约快 `6.4%`。
- 标题/正文输入的 120 ms 合并刷新移除导航和分类全量统计，只保留结果匹配、排序与预览更新；5000 条真实输入路径由 `3.066 ms` 降至 `1.038 ms`，约快 `66.1%`。
- 纯 Note 选择改走窄事件边界，不再触发整页内容刷新和第二次编辑器刷新；5000 条真实窗口切换中位数由 `1.496 ms` 降至 `0.153 ms`，约快 `89.8%`。
- 5000 条下复用稳定筛选快照，并在分类范围排序前收窄候选集；搜索变化后刷新全部范围约快 `37.9%`、分类范围约快 `84.4%`，稳定读取低于 `0.001 ms/次`。
- Note 列表改用 WPF 回收式 UI 虚拟化；3000 条首屏构造与布局中位数由约 `2522.9 ms` 降至 `32.4 ms`（约快 `98.7%`），完整结果仍保留且首屏只实现 7 行。
- 有查询时，搜索框右侧从 `Ctrl F` 提示原位切换为可点击、可键盘聚焦且带辅助技术名称的清空按钮；清空后恢复范围提示与快捷键徽标。
- 搜索结果列表支持 `Enter` 进入正文、`Esc` 清空活动查询并返回搜索框；真实窗口测试确认焦点与当前 Note 上下文保持正确。
- 应用状态使用有范围的 WPF live region：明确动作与错误可被辅助技术获知，普通自动保存状态不制造重复播报。
- 核心 WPF 控件采用统一语义模板；搜索范围、快捷键、本地保存状态、导航数量与分类数量持续可见。
- “新建”降为次级动作，“复制正文”保持活动 Note 唯一实心主操作；收藏金色化，危险操作继续独立分区。
- 紧凑、标准、宽屏和超宽布局已在 minimum、default、1080p、1200p、2K 与 4K 渲染中检查；回收站、空状态和四主题同时覆盖。
- `Ctrl+N`、标题焦点、设置窗口与 UI Automation 文本已做可见窗口检查；自动化焦点缓存不作为产品焦点真相。
- `build.ps1 -Task Verify` 一次通过，根目录双 EXE 已发布为 `1.7.0.0`；根目录 DLL 数与发布后 SeerNote 进程数均为 0。

## Next

继续以小型、可验证的交互优化推进；列表容器生命周期已冻结在 `EntryListBox`，导航聚合与失效策略已冻结在 `NavigationSnapshot` / `MainViewModel`，新的独立交互域不得重新塞回 `MainWindow`。真实鼠标滚轮/滚动条拖动、Note 拖拽、混合 DPI 多显示器拖动与中文 IME 长会话仍可补充，不阻塞当前未发布优化。

## Risks

- 3000 条机制测试与离屏渲染已覆盖数据完整性、容器数量和回收正确性；尚未执行真实鼠标滚轮、滚动条拖动与 Note 拖拽的手工长会话。
- 十二种主窗口状态已实际离屏渲染，并完成基础可见窗口交互；尚未在多台真实物理显示器间执行拖动验收。
- 尚未在混合 DPI 多显示器之间手工拖动窗口；Per-Monitor V2 manifest 与逻辑 DIP 策略已静态确认。
- 尚未以真实 junction/SUBST 别名执行双进程手工检查；规范化身份和共享锁已有自动化覆盖。
- 故障测试使用可控文件占用/写入失败，没有注入真实断电或磁盘满。
- 构建从系统 GAC 解析程序集，尚未严格钉住 .NET Framework 4.8 reference assemblies。

## Detailed Record

[大集合响应性阶段总结](progress/phases/phase-001-large-collection-responsiveness/REVIEW.md)

[分类选择器稳定项目](progress/rounds/2026-08-24-round-007-category-picker-stability.md)

[导航快照失效边界](progress/rounds/2026-08-24-round-006-navigation-invalidation.md)

[导航统计快照](progress/rounds/2026-08-24-round-005-navigation-snapshot.md)

[文本输入窄刷新](progress/rounds/2026-08-24-round-004-text-edit-refresh.md)

[Note 选择窄刷新边界](progress/rounds/2026-08-24-round-003-selection-refresh-boundary.md)

[筛选快照复用](progress/rounds/2026-08-24-round-002-filter-snapshot-reuse.md)

[Note 列表虚拟化](progress/rounds/2026-08-24-round-001-entry-list-virtualization.md)

[状态反馈 live region](progress/rounds/2026-08-23-round-004-status-live-region.md)

[搜索交互闭环优化](progress/rounds/2026-08-23-round-003-search-clear-action.md)

[1.7.0 智能体友好 UI 重设计](progress/rounds/2026-08-23-round-002-agent-friendly-ui.md)

[1.7.0 发布验收](progress/releases/v1.7.0/ACCEPTANCE.md)

[1.6.1 复制正文下移](progress/rounds/2026-08-23-round-001-copy-body-bottom.md)

[1.6.1 发布验收](progress/releases/v1.6.1/ACCEPTANCE.md)

[1.6.0 智能体 CLI 与桌面交接](progress/rounds/2026-08-22-round-001-agent-cli.md)

[变更记录](CHANGELOG.md)
