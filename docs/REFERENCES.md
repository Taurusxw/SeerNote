# 开源与平台参考

本项目只迁移公开的产品原则和平台做法，不复制第三方源码、文案、图标或辨识性布局。

| 来源 | 采用的决定 | 明确不采用 |
|---|---|---|
| [Lintalist](https://github.com/lintalist/lintalist) | 全局唤起、输入即过滤、键盘选择、提示词快速复制 | AutoHotkey 脚本执行、Bundle 插件格式、旧式界面 |
| [SnipForge](https://github.com/ArtluxDM/SnipForge) | `{{变量}}`、复制前填写、本地无账号 | Electron、远程团队库、GitHub 角色和命令军械库 |
| [PNotes](https://sourceforge.net/projects/pnotes/) | 独立便签属性、置顶小窗、免安装、不写注册表、简繁中文 | 皮肤、语音、复杂提醒与同步 |
| [FromScratch](https://github.com/Kilian/fromscratch) | 单一编辑区、自动保存、明确便携模式 | Electron 与单文档限制 |
| [tomboy-ng](https://github.com/tomboy-notes/tomboy-ng) | 本地优先、轻量分类的克制 | 双向链接与知识图谱扩张 |
| [QOwnNotes](https://github.com/pbek/QOwnNotes) | 数据可迁移、外部备份意识 | 云同步、脚本、AI/MCP、可停靠工作台 |

## Windows 与 .NET

- [.NET Framework on Windows](https://learn.microsoft.com/en-us/dotnet/framework/install/on-server-2019)：Windows 11 包含 4.8/4.8.1 兼容运行时。
- [WPF deployment](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/deploying-a-wpf-application-wpf)：目录/XCopy 发布模型。
- [WPF 应用字体打包](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/packaging-fonts-with-applications)：字体可以作为应用内容随部署目录加载，但必须先确认再分发许可。
- [High DPI desktop apps](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)：Per-Monitor V2。
- [Keyboard interactions](https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-interactions)：键盘顺序与命令行为。
- [Focus navigation](https://learn.microsoft.com/en-us/windows/apps/develop/input/focus-navigation)：方向键内部导航、逻辑焦点顺序和辅助技术共用的焦点模型。
- [AutoSuggestBox guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/auto-suggest-box)：搜索输入、清空动作、结果反馈与键盘选择的组合模式。
- [WPF live-region status sample](https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/text_livesetting)：动态状态的 `LiveSetting` 与 `LiveRegionChanged` 事件。
- [Optimizing performance: Controls](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)：WPF 列表 UI 虚拟化、容器回收及会禁用虚拟化的使用方式。
- [Improve the scrolling performance of a ListBox](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-improve-the-scrolling-performance-of-a-listbox)：`VirtualizingStackPanel` 与 `Recycling` 模式的官方配置示例。
- [ItemsControl.PrepareContainerForItemOverride](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.itemscontrol.preparecontainerforitemoverride?view=windowsdesktop-10.0) 与 [ClearContainerForItemOverride](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.itemscontrol.clearcontainerforitemoverride?view=windowsdesktop-10.0)：回收容器的准备/清理边界，所有与数据项相关的视觉、菜单、工具提示及辅助技术状态必须在这里成对维护。
- [Handle the ContextMenuOpening event](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/how-to-handle-the-contextmenuopening-event)：在菜单显示前依据事件源调整既有命令与状态；预先提供非空菜单可避开首次打开时动态替换过晚的时序问题。
- [ContextMenu.PlacementTarget](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.contextmenu.placementtarget?view=windowsdesktop-10.0)：WPF 打开附着菜单时会把拥有控件设为放置目标；共享菜单仍需从当前容器解析并冻结业务目标，不能把放置位置等同于命令身份。
- [ObservableCollection.Move](https://learn.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.observablecollection-1.move?view=netframework-4.8.1)：以一个 `Move` 集合通知表达分类重排，避免将一次移动退化为删除/插入或全量重置。
- [Optimizing performance: Data binding](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-data-binding)：重复替换 `ItemsSource` 会重新生成列表项目；稳定集合与变更通知可避免无谓重建。
- [ItemCollection](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.itemcollection?view=windowsdesktop-10.0)：WPF `ItemsControl` 依赖项目集合生成内容；`Clear` 会清空集合并释放项目引用，稳定拓扑不应反复重建。
- [Data binding overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/)：集合视图、筛选/排序层与 `IList` 数据源的 WPF 语义和性能取舍。
- [Optimizing performance: Layout and design](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-layout-and-design)：WPF 布局递归测量/排列成本，以及减少无关布局失效的官方建议。
- [DispatcherTimer](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer?view=netframework-4.8.1)：UI Dispatcher 定时器的“不早于间隔”语义、队列调度与优先级影响。
- [WPF threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)：保持 Dispatcher 工作项短小、按优先级让出输入和渲染的响应性原则。
- [CA1851: Possible multiple enumerations](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1851)：重复枚举集合的时间成本、延迟执行风险和单次物化/聚合取舍。
- [`List<T>.RemoveAll`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.removeall?view=netframework-4.8.1)：以一次 O(n) 稳定压缩删除所有匹配项并返回删除数，避免循环调用 `Remove` 导致重复线性查找与尾部搬移。
- [Writing large, responsive .NET Framework apps](https://learn.microsoft.com/en-us/dotnet/framework/performance/writing-large-responsive-apps)：以测量定位响应性瓶颈、缓存重复计算结果，并控制缓存生命周期与内存成本。
- [UI Automation overview](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview)：Windows 辅助技术语义。
- [ReplaceFile](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)：同卷原子替换语义。

## 中英文字体调研

候选只考虑免费开源、可随应用再分发、简体中文与英文同族覆盖、适合小字号 UI、无需联网或系统安装的字体。仓库社区反馈和官方配置说明用于判断字形与部署方式，许可证文本决定能否实际随包。

| 候选 | 优点 | 未采用原因 |
|---|---|---|
| [Adobe 思源黑体](https://github.com/adobe-fonts/source-han-sans) | 成熟的泛中日韩无衬线家族，官方提供 CN 区域子集 OTF；中文正文和英文 UI 可使用同一字体度量 | 仅保留 Regular 一个字重，避免多字重扩大便携包 |
| [更纱黑体](https://github.com/be5invis/Sarasa-Gothic) | UI SC 以 Inter 西文配合思源中文字形，中英混排和界面用途明确 | 单字重发布文件仍明显更大，且当前产品不需要编程字体变体 |
| [霞鹜文楷轻便版](https://github.com/lxgw/LxgwWenKai-Lite) | OFL、中文亲和力强，项目明确面向更轻量的应用与网页使用 | 正文字形带手写气质，不符合 Win11 记事本式克制无衬线方向，单字重也更大 |
| [Noto Sans CJK](https://github.com/notofonts/noto-cjk) | 与思源黑体同源，官方提供简体中文区域子集及多种部署格式 | 与思源黑体的字形覆盖高度重叠，没有为 SeerNote 带来足以抵消切换成本的收益 |

最终采用未经修改的 `SourceHanSansCN-Regular.otf`，大小 `8,429,224` bytes，SHA-256 为 `E2BC8A2E7F37474B774FFF8DB758681ECE40BB6947A90D571BCE9DD60671A8E4`。字体按 [SIL Open Font License 1.1](https://github.com/adobe-fonts/source-han-sans/blob/master/LICENSE.txt) 随软件再分发，原许可证作为 `fonts/OFL-SourceHanSans.txt` 同包提供；不对子集或字体名称做二次修改。

## 选型排除

- .NET 8 self-contained：运行时独立，但发布体积明显偏离“小而美”。
- Tauri/WebView2：可做精致界面，但引入浏览器运行时依赖和更高内存。
- Rust 直接 Win32：发布上限最小，但文本、UIA、DPI 与视觉完成成本显著提高。
- SQLite：恢复与事务成熟，但在本技术栈会增加托管/原生发布文件；当前规模不需要查询引擎。
