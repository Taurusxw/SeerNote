using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SeerNote.Domain;
using SeerNote.Platform;
using SeerNote.Presentation;
using SeerNote.Storage;
using SeerNote.Theme;

namespace SeerNote.Tests
{
    public static class PresentationTests
    {
        private const string TemporaryDirectoryPrefix = "SeerNote.PresentationTests-";

        public static void RunAll()
        {
            MainWindowUsesUnifiedNotesAndCustomCategoryLayout();
            MainWindowUsesNotepadInspiredEditorSurface();
            AgentHandoffActionsRemainAvailableForDeletedNotes();
            MainWindowAdaptsAcrossSupportedDesktopProfiles();
            MainWindowThemesAndEmptyStateRenderCoherently();
            CategorySidebarPreservesStableRows();
            SettingsDialogUsesCollapsedSecondarySections();
            StickyWindowAdaptsToContentWithinThresholds();
        }

        private static void MainWindowAdaptsAcrossSupportedDesktopProfiles()
        {
            EnsureApplication();
            var profiles = new[]
            {
                new { Name = "minimum", WorkArea = new Size(860, 540), ExpectedSize = new Size(860, 540), ExpectedClass = MainWindowLayoutClass.Compact, MinimumBodyHeight = 250.0 },
                new { Name = "default", WorkArea = new Size(1080, 720), ExpectedSize = new Size(1080, 720), ExpectedClass = MainWindowLayoutClass.Compact, MinimumBodyHeight = 400.0 },
                new { Name = "1080p", WorkArea = new Size(1920, 1040), ExpectedSize = new Size(1304, 776), ExpectedClass = MainWindowLayoutClass.Standard, MinimumBodyHeight = 400.0 },
                new { Name = "1200p", WorkArea = new Size(1920, 1160), ExpectedSize = new Size(1304, 864), ExpectedClass = MainWindowLayoutClass.Standard, MinimumBodyHeight = 400.0 },
                new { Name = "2k", WorkArea = new Size(2560, 1400), ExpectedSize = new Size(1736, 1048), ExpectedClass = MainWindowLayoutClass.Wide, MinimumBodyHeight = 400.0 },
                new { Name = "4k", WorkArea = new Size(3840, 2120), ExpectedSize = new Size(1920, 1280), ExpectedClass = MainWindowLayoutClass.UltraWide, MinimumBodyHeight = 400.0 }
            };

            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("超长分类名称用于检查宽屏布局");
                state.Entries.Add(new Entry
                {
                    Title = "多分辨率适配检查：中文标题、English title 与 1234567890",
                    Body = String.Join("\r\n", Enumerable.Repeat("正文保持可达、自动换行，并为搜索、列表和编辑区保留稳定层级。", 16)),
                    Category = state.Categories[0]
                });
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    string renderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "responsive-renders");
                    Directory.CreateDirectory(renderDirectory);
                    var content = (FrameworkElement)window.Content;
                    foreach (var profile in profiles)
                    {
                        Size startup = MainWindowLayoutCalculator.GetStartupSize(profile.WorkArea);
                        Require(startup == profile.ExpectedSize, profile.Name + " should select the expected work-area-aware startup size.");
                        MainWindowLayout layout = MainWindowLayoutCalculator.GetLayout(startup.Width);
                        Require(layout.LayoutClass == profile.ExpectedClass, profile.Name + " should select the expected responsive layout class.");

                        double contentHeight = startup.Height - 34.0;
                        content.InvalidateMeasure();
                        content.Measure(new Size(startup.Width, contentHeight));
                        content.Arrange(new Rect(0, 0, startup.Width, contentHeight));
                        content.UpdateLayout();

                        List<DependencyObject> elements = Descendants(content).ToList();
                        Grid main = elements.OfType<Grid>().Single(control => AutomationProperties.GetName(control) == "主响应式布局");
                        TextBox body = elements.OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "条目正文");
                        Require(Math.Abs(main.ColumnDefinitions[0].ActualWidth - layout.SidebarWidth) < 0.5, profile.Name + " should apply the calculated sidebar width.");
                        Require(Math.Abs(main.ColumnDefinitions[2].ActualWidth - layout.ListWidth) < 0.5, profile.Name + " should apply the calculated Note-list width.");
                        Require(main.ColumnDefinitions[4].ActualWidth >= layout.EditorMinimumWidth, profile.Name + " should preserve the editor minimum width.");
                        Require(body.ActualWidth >= layout.EditorMinimumWidth - 2.0 && body.ActualHeight >= profile.MinimumBodyHeight, profile.Name + " should keep the editing canvas usable.");

                        var bitmap = new RenderTargetBitmap((int)startup.Width, (int)contentHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(content);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (FileStream stream = File.Create(Path.Combine(renderDirectory, profile.Name + ".png")))
                        {
                            encoder.Save(stream);
                        }
                    }
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void MainWindowUsesUnifiedNotesAndCustomCategoryLayout()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("工作");
                state.Categories.Add("资料");
                state.Entries.Add(new Entry { Title = "项目开发", Body = "正文 {{名称}}", Category = "工作" });
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();

                    List<DependencyObject> elements = Descendants(content).ToList();
                    List<string> buttonNames = elements.OfType<Button>().Select(AutomationProperties.GetName).Where(name => name != null).ToList();
                    Require(buttonNames.Count(name => name == "新建条目") == 1, "The list pane should expose one unified new-Note action.");
                    Require(!buttonNames.Contains("新建随手记") && !buttonNames.Contains("新建提示词"), "Separate memo/prompt creation actions should be removed.");
                    Require(buttonNames.IndexOf("收藏置顶") >= 0 && buttonNames.IndexOf("收藏置顶") < buttonNames.IndexOf("所有条目"), "Favorite navigation should appear before all Notes.");
                    Require(elements.OfType<CategorySidebar>().Count() == 1, "The sidebar should contain one custom category navigation control.");

                    ComboBox category = elements.OfType<ComboBox>().FirstOrDefault(control => AutomationProperties.GetName(control) == "条目分类");
                    Require(category != null, "The editor should expose a category picker.");
                    Require(category.ActualWidth >= 180, "The category picker should retain a usable width in the default layout.");
                    Require(!elements.OfType<ComboBox>().Any(control => AutomationProperties.GetName(control) == "条目类型"), "Unified Notes should not expose a type selector.");
                    Require(!elements.OfType<TextBlock>().Any(control => String.Equals(control.Text, "变量占位：{{名称}}", StringComparison.Ordinal)), "The editor should not reserve a competing always-visible variable hint above the body.");

                    ListBox results = elements.OfType<ListBox>().First(control => AutomationProperties.GetName(control) == "条目结果");
                    var noteItem = results.Items[0] as ListBoxItem;
                    Require(noteItem != null && noteItem.ContextMenu != null, "Each Note should expose a context menu.");
                    List<string> menuHeaders = noteItem.ContextMenu.Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();
                    Require(menuHeaders.Contains("复制正文") && menuHeaders.Contains("复制 Note ID") && menuHeaders.Contains("复制为 JSON") && menuHeaders.Contains("收藏置顶") && menuHeaders.Contains("打开置顶小窗") && menuHeaders.Contains("移动到分类") && menuHeaders.Contains("移到回收站"), "The Note context menu should contain both human and agent handoff actions.");
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void MainWindowUsesNotepadInspiredEditorSurface()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("工作");
                state.Entries.Add(new Entry
                {
                    Title = "很长的中文 Note 标题用于检查标签宽度和最小窗口版式",
                    Body = "第一行正文\r\n第二行正文",
                    Category = "工作"
                });
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();

                    List<DependencyObject> elements = Descendants(content).ToList();
                    Border documentTab = elements.OfType<Border>().Single(control => AutomationProperties.GetName(control) == "当前 Note 标签");
                    Border commandBar = elements.OfType<Border>().Single(control => AutomationProperties.GetName(control) == "Note 命令栏");
                    Border bodySurface = elements.OfType<Border>().Single(control => AutomationProperties.GetName(control) == "纯文本编辑画布");
                    Border bottomBar = elements.OfType<Border>().Single(control => AutomationProperties.GetName(control) == "Note 底部操作区");
                    StackPanel agentHandoff = elements.OfType<StackPanel>().Single(control => AutomationProperties.GetName(control) == "智能体交接操作区");
                    WrapPanel destructiveActions = elements.OfType<WrapPanel>().Single(control => AutomationProperties.GetName(control) == "Note 删除操作区");
                    TextBox title = elements.OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "条目标题");
                    TextBox body = elements.OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "条目正文");
                    TextBlock saveState = elements.OfType<TextBlock>().Single(control => AutomationProperties.GetName(control) == "当前 Note 保存状态");
                    TextBlock searchPlaceholder = elements.OfType<TextBlock>().Single(control => control.Text == "搜索 Note…");

                    Require(documentTab.Child == title, "The active document tab should own the editable Note title.");
                    Require(bodySurface.Child == body, "The paper surface should be the direct host of the plain-text editor.");
                    Require(body.BorderThickness == new Thickness(0) && body.Padding.Left >= 20, "The plain-text canvas should stay borderless with a readable page margin.");
                    Require(Object.ReferenceEquals(body.Background, Application.Current.FindResource(ThemeResources.SurfaceBrushKey)), "The editor canvas should use the semantic paper surface.");
                    Require(saveState.Text == "已保存到本地" && saveState.Visibility == Visibility.Visible, "The editor header should keep local save truth visible beside the active Note.");
                    Require(searchPlaceholder.Visibility == Visibility.Visible, "The empty search field should expose a visible scope hint without replacing its automation name.");

                    List<string> commandNames = Descendants(commandBar).OfType<Button>().Select(AutomationProperties.GetName).Where(name => name != null).ToList();
                    List<string> agentNames = Descendants(agentHandoff).OfType<Button>().Select(AutomationProperties.GetName).Where(name => name != null).ToList();
                    List<string> destructiveNames = Descendants(destructiveActions).OfType<Button>().Select(AutomationProperties.GetName).Where(name => name != null).ToList();
                    Require(!commandNames.Contains("复制正文") && commandNames.Contains("打开置顶小窗") && commandNames.Contains("切换收藏置顶"), "The compact command bar should keep classification, favorite, and sticky-window actions without copy body.");
                    Require(agentNames.SequenceEqual(new[] { "复制正文", "复制 Note ID", "复制为 JSON" }), "The bottom-left handoff group should expose body, ID, and JSON copy actions in a predictable order.");
                    Require(!commandNames.Contains("移到回收站") && destructiveNames.Contains("移到回收站"), "Destructive Note actions should remain separated from the frequent command bar.");
                    Require(Descendants(bottomBar).OfType<Button>().Count(button => AutomationProperties.GetName(button) == "复制正文") == 1, "The bottom action bar should own the only visible copy-body button.");
                    Style primaryStyle = (Style)Application.Current.FindResource("Seer.PrimaryButton");
                    Style toolbarStyle = (Style)Application.Current.FindResource("Seer.ToolbarButton");
                    List<Button> visiblePrimaryActions = Descendants(bottomBar).OfType<Button>().Where(button => button.Visibility == Visibility.Visible && Object.ReferenceEquals(button.Style, primaryStyle)).ToList();
                    Require(visiblePrimaryActions.Count == 1 && AutomationProperties.GetName(visiblePrimaryActions[0]) == "复制正文", "An active Note should expose exactly one solid primary action: copy body.");
                    Require(elements.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "新建条目").Style == toolbarStyle, "New Note should remain prominent but secondary while an editable Note is active.");

                    content.InvalidateMeasure();
                    content.Measure(new Size(860, 506));
                    content.Arrange(new Rect(0, 0, 860, 506));
                    content.UpdateLayout();
                    Require(body.ActualWidth >= 340 && body.ActualHeight >= 250, "The editing canvas should remain usable at the supported minimum window size; actual=" + body.ActualWidth + "×" + body.ActualHeight + ".");
                    Require(documentTab.ActualWidth >= 200 && documentTab.ActualWidth <= 420, "The document tab should keep bounded, readable geometry for long Chinese titles.");
                    Require(agentHandoff.ActualWidth > 0 && agentHandoff.ActualHeight > 0 && destructiveActions.ActualWidth > 0, "Agent and destructive action groups should remain reachable at the minimum size.");
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void AgentHandoffActionsRemainAvailableForDeletedNotes()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Settings.LastSmartView = SmartView.Trash;
                state.Entries.Add(new Entry
                {
                    Title = "待恢复资料",
                    Body = "回收站内容仍需提供给智能体检查。",
                    IsDeleted = true,
                    DeletedUtc = DateTime.UtcNow
                });
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();

                    List<DependencyObject> elements = Descendants(content).ToList();
                    Button copyId = elements.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "复制 Note ID");
                    Button copyJson = elements.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "复制为 JSON");
                    Button copyBody = elements.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "复制正文");
                    Button restore = elements.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "还原条目");
                    Button permanentDelete = elements.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "永久删除条目");
                    TextBlock saveState = elements.OfType<TextBlock>().Single(control => AutomationProperties.GetName(control) == "当前 Note 保存状态");
                    Require(copyId.Visibility == Visibility.Visible && copyJson.Visibility == Visibility.Visible, "Deleted Notes should retain agent handoff actions.");
                    Require(copyBody.Visibility == Visibility.Collapsed, "Deleted Notes should keep the copy-body action unavailable.");
                    Require(restore.Visibility == Visibility.Visible && permanentDelete.Visibility == Visibility.Visible, "Deleted Notes should keep recovery and explicit permanent-delete actions.");
                    Require(saveState.Text == "只读 · 回收站", "Deleted Notes should replace editable save truth with an explicit read-only recovery state.");

                    ListBox results = elements.OfType<ListBox>().First(control => AutomationProperties.GetName(control) == "条目结果");
                    var noteItem = results.Items[0] as ListBoxItem;
                    Require(noteItem != null && noteItem.ContextMenu != null, "Deleted Notes should expose a context menu.");
                    List<string> menuHeaders = noteItem.ContextMenu.Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();
                    Require(menuHeaders.Contains("复制 Note ID") && menuHeaders.Contains("复制为 JSON") && menuHeaders.Contains("还原 Note") && menuHeaders.Contains("永久删除"), "Deleted Note menus should preserve handoff and recovery actions.");
                    Require(!menuHeaders.Contains("复制正文"), "Deleted Notes should not expose the human copy-body command.");

                    string renderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "responsive-renders");
                    Directory.CreateDirectory(renderDirectory);
                    var bitmap = new RenderTargetBitmap(1080, 686, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (FileStream stream = File.Create(Path.Combine(renderDirectory, "deleted.png")))
                    {
                        encoder.Save(stream);
                    }
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void CategorySidebarPreservesStableRows()
        {
            EnsureApplication();
            var sidebar = new CategorySidebar();
            IList<string> categories = new List<string> { "Codex", "资料" };
            sidebar.Refresh(categories, "Codex", new Dictionary<string, int> { { "Codex", 1 }, { "资料", 0 } });
            ListBox list = Descendants(sidebar).OfType<ListBox>().Single();
            var originalFirst = (ListBoxItem)list.Items[0];

            sidebar.Refresh(categories, "资料", new Dictionary<string, int> { { "Codex", 2 }, { "资料", 1 } });
            Require(Object.ReferenceEquals(originalFirst, list.Items[0]), "Selection-only refreshes should preserve category containers instead of clearing and rebuilding the sidebar.");
            var row = originalFirst.Content as Grid;
            var count = row != null && row.Children.Count > 1 ? row.Children[1] as TextBlock : null;
            Require(count != null && count.Text == "2", "Stable category rows should still update their counts in place.");
        }

        private static void MainWindowThemesAndEmptyStateRenderCoherently()
        {
            EnsureApplication();
            ResourceDictionary themeResources = Application.Current.Resources.MergedDictionaries.FirstOrDefault(dictionary => dictionary.Contains(ThemeResources.CanvasBrushKey));
            Require(themeResources != null, "Presentation rendering requires the live semantic theme dictionary.");
            string renderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "responsive-renders");
            Directory.CreateDirectory(renderDirectory);

            try
            {
                foreach (AppTheme theme in Enum.GetValues(typeof(AppTheme)))
                {
                    ThemeResources.ApplyTheme(themeResources, theme);
                    WithTemporaryDirectory(delegate(string root)
                    {
                        var state = new AppState();
                        state.Entries.Add(new Entry
                        {
                            Title = "主题渲染 · " + theme,
                            Body = "本地正文与按钮、搜索、列表、保存状态共享同一语义主题。",
                            IsFavorite = true
                        });
                        var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                        var window = new MainWindow(viewModel);
                        try
                        {
                            var content = (FrameworkElement)window.Content;
                            content.Measure(new Size(1080, 686));
                            content.Arrange(new Rect(0, 0, 1080, 686));
                            content.UpdateLayout();
                            Render(content, 1080, 686, Path.Combine(renderDirectory, "theme-" + theme.ToString().ToLowerInvariant() + ".png"));
                        }
                        finally
                        {
                            window.Dispose();
                        }
                    });
                }
            }
            finally
            {
                ThemeResources.ApplyTheme(themeResources, AppTheme.Graphite);
            }

            WithTemporaryDirectory(delegate(string root)
            {
                var viewModel = new MainViewModel(new AppState(), new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();
                    List<DependencyObject> elements = Descendants(content).ToList();
                    Require(elements.OfType<TextBlock>().Any(control => control.Text == "选择一条 Note 开始编辑" && control.Visibility == Visibility.Visible), "The empty editor should explain the next valid action.");
                    Require(elements.OfType<Button>().Any(button => AutomationProperties.GetName(button) == "从空状态新建条目"), "The empty library should retain a direct creation action.");
                    Render(content, 1080, 686, Path.Combine(renderDirectory, "empty.png"));
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void SettingsDialogUsesCollapsedSecondarySections()
        {
            EnsureApplication();
            var dialog = new SettingsDialog(CloseButtonBehavior.MinimizeToTray, AppTheme.Sage);
            List<Expander> sections = Descendants((DependencyObject)dialog.Content).OfType<Expander>().ToList();
            Require(sections.Count == 2, "Settings should contain exactly two secondary disclosure sections.");
            Require(sections.All(section => !section.IsExpanded), "Secondary settings sections should start collapsed.");
            Require(sections.Any(section => AutomationProperties.GetName(section).Contains("鼠尾草")), "Theme section summary should expose the current selection.");
            Require(sections.Any(section => AutomationProperties.GetName(section).Contains("最小化到托盘")), "Close behavior section summary should expose the current selection.");
        }

        private static void StickyWindowAdaptsToContentWithinThresholds()
        {
            EnsureApplication();
            var desktop = new Size(1920, 1040);
            Size maximum = StickyWindowSizeCalculator.GetMaximumSize(desktop);
            Size shortSize = StickyWindowSizeCalculator.Calculate("短标题", "一行正文", desktop);
            Require(shortSize.Width == StickyWindowSizeCalculator.MinimumWidth, "Short sticky notes should use the minimum width.");
            Require(shortSize.Height == StickyWindowSizeCalculator.MinimumHeight, "Short sticky notes should use the minimum height.");

            string mediumBody = String.Concat(Enumerable.Repeat("这是用于测量中文换行的中等长度正文。", 12));
            Size mediumSize = StickyWindowSizeCalculator.Calculate("项目记录", mediumBody, desktop);
            Require(mediumSize.Width > shortSize.Width || mediumSize.Height > shortSize.Height, "Medium content should grow beyond the minimum size.");
            Require(mediumSize.Width <= maximum.Width && mediumSize.Height <= maximum.Height, "Medium content must remain inside the configured maximum size.");

            string longBody = String.Concat(Enumerable.Repeat("超长正文需要先扩宽、再增高，达到阈值后使用滚动条。", 250));
            Size longSize = StickyWindowSizeCalculator.Calculate("长篇记录", longBody, desktop);
            Require(longSize.Height == maximum.Height, "Oversized content should stop at the maximum height.");
            Require(longSize.Width <= maximum.Width, "Oversized content must not exceed the maximum width.");

            var compactWorkArea = new Size(500, 400);
            Size compactMaximum = StickyWindowSizeCalculator.GetMaximumSize(compactWorkArea);
            Size compactSize = StickyWindowSizeCalculator.Calculate("小工作区", longBody, compactWorkArea);
            Require(compactSize.Width <= compactMaximum.Width && compactSize.Height <= compactMaximum.Height, "Adaptive sizing should also respect a smaller work area.");

            var entry = new Entry { Title = "短标题", Body = "一行正文" };
            entry.Sticky.Left = 120;
            entry.Sticky.Top = 80;
            entry.Sticky.Width = 700;
            entry.Sticky.Height = 600;
            var window = new StickyWindow(entry);
            try
            {
                Require(window.Width == shortSize.Width && window.Height == shortSize.Height, "Opening a sticky note should recalculate size from content instead of restoring stale dimensions.");
                Require(window.MinWidth == StickyWindowSizeCalculator.MinimumWidth && window.MinHeight == StickyWindowSizeCalculator.MinimumHeight, "Sticky windows should expose the configured minimum thresholds.");
                Require(window.MaxWidth <= StickyWindowSizeCalculator.MaximumWidth && window.MaxHeight <= StickyWindowSizeCalculator.MaximumHeight, "Sticky windows should expose work-area-aware maximum thresholds.");
                var editor = window.Content as TextBox;
                Require(editor != null && editor.TextWrapping == TextWrapping.Wrap, "Sticky content should wrap instead of growing horizontally without limit.");
                Require(editor.VerticalScrollBarVisibility == ScrollBarVisibility.Auto && editor.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled, "Only vertical overflow should scroll after reaching the maximum size.");

                editor.Text = longBody;
                DrainDispatcher(TimeSpan.FromMilliseconds(260));
                Size expectedLong = StickyWindowSizeCalculator.Calculate(entry.DisplayTitle, longBody, SystemParameters.WorkArea.Size);
                Require(window.Width == expectedLong.Width && window.Height == expectedLong.Height, "Editing content should coalesce into a new adaptive size while no manual override is active.");
            }
            finally
            {
                window.Close();
                DrainDispatcher(TimeSpan.Zero);
            }
        }

        private static void EnsureApplication()
        {
            if (Application.Current == null)
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources.MergedDictionaries.Add(ThemeResources.Create());
            }
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            if (root == null)
            {
                yield break;
            }
            yield return root;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                var dependencyChild = child as DependencyObject;
                if (dependencyChild == null)
                {
                    continue;
                }
                foreach (DependencyObject descendant in Descendants(dependencyChild))
                {
                    yield return descendant;
                }
            }
        }

        private static void DrainDispatcher(TimeSpan duration)
        {
            var frame = new DispatcherFrame();
            if (duration > TimeSpan.Zero)
            {
                var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
                {
                    Interval = duration
                };
                timer.Tick += delegate
                {
                    timer.Stop();
                    frame.Continue = false;
                };
                timer.Start();
            }
            else
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { frame.Continue = false; }));
            }
            Dispatcher.PushFrame(frame);
        }

        private static void Render(FrameworkElement content, int width, int height, string path)
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            string root = Path.Combine(Path.GetTempPath(), TemporaryDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                action(root);
            }
            finally
            {
                string full = Path.GetFullPath(root);
                string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
                if (full.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(full).StartsWith(TemporaryDirectoryPrefix, StringComparison.Ordinal))
                {
                    Directory.Delete(full, true);
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
