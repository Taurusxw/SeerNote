# SeerNote 架构

## 1. 质量属性顺序

1. 数据不丢失、错误可恢复。
2. 中文输入、编辑、搜索和复制正确。
3. 启动、搜索和保存感知迅速。
4. 发布目录小、无安装、可整体迁移。
5. UI 具有一致视觉层级与键盘可达性。

“最快、最好、最小、最兼容”转化为可测边界：冷启动目标小于 1 秒、1,000 条搜索目标小于 50 ms、发布程序不引入第三方运行时文件、Windows 11 标准文本输入可用、关键状态实际渲染。

## 2. 技术决策

- `.NET Framework 4.8`：Windows 11 自带兼容运行时，发布不捆绑几十 MB 运行时。
- WPF：标准文本、绑定、DPI、键盘和 UI Automation 能力成熟，视觉系统无需 WebView。
- AnyCPU：避免本机原生 DLL，减少 x64/ARM64 发布分叉。
- 零第三方包：避免 NuGet 供应链、原生 SQLite DLL 和版本漂移。
- 原子 JSON：数据规模小，完整快照便于备份、人工查看和迁移。

详细权衡见 ADR-0001 与 ADR-0002。

## 3. 模块

```text
SeerNote.exe / SeerNote.App
├─ Agent
│  └─ AgentNotePayload / AgentJson
├─ Cli
│  └─ CliApplication
├─ Domain
│  ├─ Entry / AppState / UserSettings / ordered categories
│  ├─ EntrySearch
│  └─ PromptTemplate
├─ Storage
│  ├─ PortableStore
│  ├─ AtomicFile
│  └─ RecoveryReport
├─ Platform
│  ├─ SingleInstanceGuard
│  ├─ GlobalHotkeyService
│  ├─ TrayIconService
│  └─ ClipboardService
├─ Presentation
│  ├─ MainViewModel
│  ├─ MainWindow
│  ├─ EntryListBox / EntryListRow / EntryContextMenu
│  ├─ NavigationSnapshot
│  ├─ MainWindowLayoutCalculator
│  ├─ CategorySidebar / CategoryDialog / VariableDialog
│  └─ StickyWindow / StickyWindowManager / StickyWindowSizeCalculator
└─ Theme
   ├─ ThemeResources / ThemePaletteState
   ├─ MenuThemeResources
   └─ AppTypography

SeerNote.Cli.exe
└─ Program -> sibling SeerNote.exe -> CliApplication.Run
```

### 深模块接口

`PortableStore` 对调用者只暴露：

```text
Load() -> LoadResult
Save(AppState) -> SaveResult
Export(path, AppState)
```

原子替换、备份保留、验证、恢复顺序和故障报告全部隐藏在内部。当前只有一个存储 Adapter，因此不创建空泛的 `IRepository`。

`PromptTemplate` 暴露：

```text
Parse(text) -> ordered unique variables
Render(text, values) -> rendered text or validation error
```

`EntrySearch` 是纯函数模块；调用者只提交条目、查询和智能视图，不了解排序与匹配细节。

`CliApplication` 把参数解析、校验、工作区锁、加载/保存、错误映射和 JSON 输出隐藏在一个进程级接口后：

```text
Run(args, stdin, stdout, stderr, applicationRoot) -> exitCode
```

控制台入口只提供标准流和可执行文件目录，不重复命令分派。自动化测试与真实 `SeerNote.Cli.exe` 穿过同一 seam。机器 envelope 使用 `seernote.cli.v1`，Note 数据使用 `seernote.note.v1`；两者独立于权威存储 `schemaVersion: 2`，不会把内部 `StickyState` 或保存实现暴露给调用者。

`StickyWindowSizeCalculator` 接收标题、正文和工作区尺寸，在 `320×180` 至 `720×640` 的阈值内测量候选宽度的真实 WPF 中文换行高度。算法优先选择能完整显示且面积较小、纵横比不过分狭长的尺寸；若所有候选均超高，则选择纵向溢出最少的宽度并封顶高度。`StickyWindow` 只负责调度测量、尊重当前会话的手动调整并保存最终位置，不把尺寸算法扩散到主窗口或数据层。

`MainWindowLayoutCalculator` 是不依赖窗口句柄的纯布局策略：首次启动从可用工作区计算 `1080×720` 至 `1920×1280` DIPs 的目标尺寸；运行时把当前内容宽度映射为紧凑、标准、宽屏或超宽三栏参数。四档侧栏/列表宽度依次为 `176/304`、`200/360`、`228/420` 与 `252/480` DIPs，编辑区最低宽度同时从 `360` 增长到 `680` DIPs。`MainWindow` 只应用列宽并继续保存用户边界，已有有效尺寸不因升级被强制覆盖。

`AppTypography` 从程序集同目录的 `fonts/` 加载应用私有 `SourceHanSansCN-Regular.otf`，把唯一的字体家族资源交给主题、编辑器、菜单和置顶小窗文本测量共用。字体不注册到 Windows；文件缺失、不可读或无法解析时回退 `Segoe UI` 并向主窗口报告状态，不因字体资产故障阻止用户读取和导出 Note。

## 4. 数据模型

```json
{
  "schemaVersion": 2,
  "savedUtc": "2026-08-18T00:00:00.0000000Z",
  "settings": {
    "globalHotkey": "Ctrl+Shift+Space",
    "windowBounds": { "left": 160, "top": 100, "width": 1080, "height": 720 },
    "lastSmartView": "all"
  },
  "categories": ["工作", "资料"],
  "entries": [
    {
      "id": "guid",
      "title": "今日待办",
      "body": "……",
      "category": "工作",
      "isFavorite": false,
      "isDeleted": false,
      "sticky": { "isOpen": false, "left": 0, "top": 0, "width": 360, "height": 260 },
      "createdUtc": "...",
      "updatedUtc": "..."
    }
  ]
}
```

版本 2 用根级有序 `categories` 保存可为空的自定义分类，Entry 只保存分类名称，不再保存随手记/提示词类型。读取版本 1 时，存储层从旧 Entry 分类生成去重有序列表，把 `memo`/`prompt` 视图归一为 `all`，正文、收藏、删除和置顶状态保持不变；首次版本 2 保存通过原子替换生成版本 1 主文件备份。未知字段在同一 schema 内允许忽略；后续 `schemaVersion` 提升仍必须有迁移和回滚证据。

## 5. 保存协议

1. 在内存中创建不可变保存快照。
2. 序列化到 `data/notes.json.tmp`，使用 UTF-8 无 BOM。
3. 对文件执行 `Flush(true)`，重新读取并反序列化验证版本与条目数。
4. 若主文件存在，使用 Windows `ReplaceFile` 语义把临时文件替换为主文件，并更新 `notes.json.bak`。
5. 首次保存使用同卷移动。
6. 成功后更新 UI 保存状态；失败时权威文件保持原状，内存内容继续存在。

每个自然日首次成功修改前建立 `data/backups/notes-YYYYMMDD-HHmmss.json`，最多保留 10 份；清理只删除模式严格匹配且超出保留数的备份。

## 6. 启动恢复

候选顺序：

1. `notes.json`
2. `notes.json.bak`
3. 完整可解析且时间更新的 `notes.json.tmp`
4. `backups/` 中从新到旧的有效文件

每个候选必须满足：可反序列化、schema 受支持、ID 唯一、必要字段有效。若主文件失败而其他候选成功，原主文件移动到带时间戳的 `data/recovery/` 后再恢复；不得覆盖唯一损坏证据。

## 7. 并发与生命周期

- 按规范化应用目录身份生成的命名 Mutex 保证常规单实例；同目录 `data/.seernote.lock` 的排它文件锁同时约束不同身份别名指向同一数据目录的写入者。
- CLI 的所有数据命令（包括读取）先取得同一个 `.seernote.lock`，因为 `PortableStore.Load()` 可能执行恢复写入；桌面应用或另一条 CLI 命令持锁时返回退出码 4，不读取或修改数据。
- 第二实例通过注册窗口消息请求第一个实例显示并聚焦，然后退出。
- UI 编辑使用 350 ms debounce；切换条目、最小化到托盘和正常退出前立即 flush。关闭主窗口按持久化设置分派到完整退出或托盘驻留路径。
- 保存任务串行化；较旧快照完成后如发现新版本，立即排队最新快照。
- 置顶小窗和主编辑器共享同一个内存条目，不创建副本。

## 8. 搜索与排序

- 规范化查询：Trim，使用 `InvariantCultureIgnoreCase` 子串匹配标题、正文和分类。
- 收藏置顶、所有条目和回收站系统视图先过滤；自定义分类在系统视图后过滤，查询再匹配。
- 排序：未删除前置、Favorite 前置、最近更新前置；回收站按删除时间倒序。
- 结果计算纯内存完成；1,000 条目标低于 50 ms。

自定义分类的顺序由 `AppState.Categories` 权威保存。分类重命名或删除会原子更新全部 Entry 引用；删除分类只把所属 Note 移到未分类，不删除正文。`CategorySidebar` 独立负责分类右键菜单、分类拖拽排序和 Note 投放目标，`MainViewModel` 负责校验、状态修改与持久化。

## 9. 平台集成

- 全局热键：`RegisterHotKey`；失败不阻断启动。
- 托盘：使用 `System.Windows.Forms.NotifyIcon`，只含“显示 SeerNote”“新建”“退出”。
- 剪贴板：WPF `Clipboard.SetText`，重试短暂占用，失败给可恢复错误。
- 置顶：WPF `Topmost=true`；位置限制在当前工作区内。
- DPI：应用 manifest 声明 Per-Monitor V2，布局使用设备无关单位。
- 设置不写注册表；选择“彻底退出”或执行退出命令时，托盘、热键和单实例锁均在进程结束前释放。“最小化到托盘”会按用户选择保留这些资源。

## 10. 视觉系统

语义 token 集中在代码式 WPF 的 `Theme/` 模块：Canvas、Surface、SurfaceRaised、Ink、Muted、Border、Accent、AccentHover、AccentInk、Gold、Success、Warning、Danger、Focus。`ThemeResources` 同时拥有按钮、输入框、列表行、滚动条与组合框的核心模板，并提供 Primary、Quiet、Toolbar、Navigation 与 Danger 按钮角色；模板保留原生 `PART_ContentHost`、IME、键盘和 UI Automation 语义。石墨、午夜、雾白和鼠尾草只映射语义角色，功能视图不能散落主题专属颜色。语义画刷的 `Color` 绑定到可通知的主题颜色状态，使其在 WPF 样式密封后仍不可冻结；切换主题只更新颜色状态，保持画刷引用，不重建窗口或丢失编辑状态。设置使用二级折叠组；分类选择、下拉项和右键菜单使用同一语义主题。旧设置默认石墨，高对比模式由 Windows 系统颜色接管。本机没有 .NET Framework targeting pack/XAML 编译目标，因此构建直接使用 Visual Studio Roslyn 与系统 WPF 程序集；这不改变运行时或标准控件行为。

`MainWindow` 继续作为代码式 WPF 的展示组合根，不接收新的存储、领域或平台职责。当前重设计把布局策略留在 `MainWindowLayoutCalculator`、分类选择/菜单/拖放编排留在 `CategorySidebar`、分类集合与视觉容器生命周期留在 `CategoryListBox`、Note 列表的虚拟化容器生命周期留在 `EntryListBox`、语义控件语法留在 `ThemeResources`；后续若再增加独立交互域，必须优先抽取有行为测试保护的展示边界，而不是继续扩大组合根。

`EntryListBox` 以 `Entry` 数据项作为 `ItemsSource`，启用 `VirtualizingStackPanel`、逻辑滚动和 `Recycling`。它在容器准备与清理阶段统一刷新或释放标题、正文预览、分类时间、收藏标记、右键菜单、工具提示与 UI Automation 名称；`MainWindow` 只持有筛选结果、选择、拖拽和业务动作，不能重新按结果总数创建 `ListBoxItem`。这条边界同时防止回收容器串行和组合根继续吸收独立展示职责。

`EntryListBox` 对 Note 右键菜单使用同一可见容量边界：1–6 条保留直接菜单，第 7 条起由列表实例分别缓存一棵活动菜单和一棵回收站菜单，避免为每个回收容器重复建立分类子菜单。`ContextMenuOpening` 必须先从事件源容器解析并同步精确 `Entry`；直接菜单只在本次打开期间保存该目标，共享 `EntryContextMenu` 同时刷新收藏文案和分类勾选。两种路径的命令都消费稳定目标，后续选择变化不能把复制、收藏、删除或还原重定向到另一条 Note。命令执行或菜单关闭后立即释放目标引用，列表缓存本身随 `EntryListBox` 生命周期回收。

`CategoryListBox` 按可见容量自适应：不超过 6 个、无需滚动的分类继续复用轻量直接行；第 7 个起改用稳定 `ObservableCollection` 数据项、单次 `Move` 通知、逻辑滚动与 `Recycling`，只为视口附近生成 `ListBoxItem`。容器准备/清理必须同步分类身份、计数、Tooltip、共享菜单、投放边框和 UI Automation 名称，回收容器不能保留前一分类状态。零分类不创建不可见列表；共享菜单壳随首个分类创建，两个命令到首次打开时才实现。`CategorySidebar` 只消费这条边界提供的当前分类与容器映射，不自行维护第二份行集合。

`MainViewModel` 的展示通知按影响范围分流：`ContentChanged` 表示 Note 数据、筛选范围或持久状态可能改变，需要刷新导航、分类、结果和编辑器；`SelectedEntryChanged` 只表示当前 Note 身份变化，`MainWindow` 仅同步列表选择、编辑器和文档状态；`StatusChanged` 只刷新保存/动作反馈。纯选择不能借用通用内容事件触发整页刷新。

标题与正文的 `TextChanged` 在编辑重入边界内更新领域对象，并以 `DispatcherPriority.Background` 的 120 ms `DispatcherTimer` 合并结果列表刷新；该 Tick 只重新计算可能受文字与 `UpdatedUtc` 影响的筛选、排序、标题和正文预览。智能视图计数与分类计数不依赖标题/正文，不能进入这条高频 Tick；自动保存由 `MainViewModel` 独立的约 350 ms 定时器负责。

`NavigationSnapshot` 拥有导航聚合规则：一次枚举同时计算活跃、收藏、回收站和按修剪名称合并的分类计数，并复制自定义分类顺序为只读内容。`MainViewModel` 缓存唯一的当前快照，只有新建、收藏、删除/恢复、移动与分类增删改序等会改变导航成员或顺序的动作才使它失效；标题/正文、置顶小窗内容、搜索词和纯导航选择继续复用。`TrashCount` 与 `MainWindow` 消费同一快照，窗口在快照引用与当前选择均未变化时直接返回，不再为证明“没变”扫描全部 Note；重建后的内容等价判断仍避免无谓控件写入。

编辑器分类选择器同样消费 `NavigationSnapshot` 的有序分类，并由快照提供独立于计数的精确顺序等价判断。Note 切换、搜索、收藏或计数变化但分类顺序未变时，只更新 `SelectedIndex`，不能清空并重加 `ComboBox.Items`；分类创建、重命名、删除或重排时仍完整重建一次，确保显示文本、顺序和当前 Note 分类同步。

中英文排版统一使用思源黑体 CN Regular：界面、编辑正文和 WPF `FormattedText` 尺寸计算共享同一个应用私有字体实例，避免显示字体与置顶小窗测量字体不同步。仅菜单勾选符号继续使用系统 `Segoe UI Symbol`；它是图形符号，不参与 Note 正文排版。

石墨主题参考起点：

```text
Canvas       #17191C
Surface      #202327
SurfaceRaised#292D32
Ink          #F2F0EA
Muted        #A5ABB3
Border       #3A3F45
Accent       #62B7AE
Gold         #C8A66A
Danger       #E47A70
Focus        #7ACAC2
```

## 11. 安全与隐私边界

- 无网络代码、遥测、自动更新、账号和远程内容。
- 所有文本视为用户数据，不执行其中的脚本或链接。
- CLI 只解析已记录的选项，不执行 Note 正文、shell 片段或外部命令；`delete` 仅软删除，永久删除继续由桌面端明确确认。
- 不宣称加密；敏感密钥不应保存在 SeerNote。
- 永久删除确认精确显示标题；备份仍可能含历史内容，文案必须说明。

## 12. 兼容与性能预算

- OS：受支持的 Windows 11 版本。
- 运行时：系统自带 .NET Framework 4.8/4.8.1。
- UI：100%、150%、200% DPI；1080p、1200p、2560×1440、3840×2160 工作区；中文微软拼音 IME。
- 数据：1,000 条为常规基准，5,000 条为压力边界。
- 列表：筛选结果保留完整数据集合，展示层只实现视口附近容器；3000 条回归样本必须验证回收后的可访问名称与操作菜单仍对应当前 Note。
- 根发布文件：`SeerNote.exe` 与 `SeerNote.Cli.exe`，不引入第三方 DLL；每个 EXE 目标小于 5 MiB。
- 必要便携分发：两个 EXE、一个 Regular 字重和 OFL 许可证合计小于 10 MiB；CLI 通过同目录主 EXE 复用领域、存储和契约实现，不新增 DLL、字体运行时或安装器。
- 空闲内存目标：小于 100 MiB；不以未测数字作为发布承诺。
