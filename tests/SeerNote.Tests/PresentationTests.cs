using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
            NavigationSnapshotAggregatesEntriesInOnePolicy();
            SearchResultsCompleteTheKeyboardFocusLoop();
            EntryListVirtualizesLargeCollections();
            EntryContextMenusAreSharedAndRetargeted();
            EntryContextMenuSharingAdaptsAtViewportThreshold();
            StatusFeedbackExposesScopedLiveRegions();
            AgentHandoffActionsRemainAvailableForDeletedNotes();
            MainWindowAdaptsAcrossSupportedDesktopProfiles();
            MainWindowThemesAndEmptyStateRenderCoherently();
            CategorySidebarPreservesStableRows();
            CategorySidebarReordersExistingRows();
            CategorySidebarVirtualizesScrollableCollections();
            CategoryPickerPreservesStableItems();
            SettingsDialogUsesCollapsedSecondarySections();
            StickyWindowAdaptsToContentWithinThresholds();
        }

        private static void NavigationSnapshotAggregatesEntriesInOnePolicy()
        {
            var entries = new List<Entry>
            {
                null,
                new Entry { Category = " 工作 ", IsFavorite = true },
                new Entry { Category = "工作" },
                new Entry { Category = "   " },
                new Entry { Category = "工作", IsFavorite = true, IsDeleted = true, DeletedUtc = DateTime.UtcNow }
            };

            var categories = new List<string> { "工作", "空分类" };
            NavigationSnapshot snapshot = NavigationSnapshot.Create(entries, categories);
            Require(snapshot.AllCount == 3, "Navigation counts should include every active Note exactly once.");
            Require(snapshot.FavoriteCount == 1, "Favorite navigation count should exclude deleted Notes.");
            Require(snapshot.TrashCount == 1, "Trash navigation count should include deleted Notes exactly once.");
            Require(snapshot.CategoryCounts.Count == 1 && snapshot.CategoryCounts["工作"] == 2, "Category counts should trim names, merge case-insensitively and exclude deleted Notes.");
            Require(snapshot.Categories.SequenceEqual(categories), "Navigation snapshot should preserve custom category order, including empty categories.");
            Require(snapshot.HasSameContent(NavigationSnapshot.Create(entries, categories)), "Equivalent navigation inputs should produce content-equivalent snapshots.");
            Require(!snapshot.HasSameContent(NavigationSnapshot.Create(entries, categories.AsEnumerable().Reverse())), "Category order changes should invalidate navigation content equivalence.");
            NavigationSnapshot changedCounts = NavigationSnapshot.Create(entries.Concat(new[] { new Entry { Category = "工作" } }), categories);
            Require(snapshot.HasSameCategoryOrder(changedCounts) && !snapshot.HasSameContent(changedCounts), "Category-order equivalence should stay independent from changing navigation counts.");
            Require(!snapshot.HasSameCategoryOrder(NavigationSnapshot.Create(entries, categories.AsEnumerable().Reverse())), "Category-order equivalence should preserve exact custom order.");

            bool readOnly = false;
            try
            {
                snapshot.CategoryCounts["工作"] = 99;
            }
            catch (NotSupportedException)
            {
                readOnly = true;
            }
            Require(readOnly, "Navigation category counts should expose a read-only snapshot.");

            readOnly = false;
            try
            {
                snapshot.Categories[0] = "修改";
            }
            catch (NotSupportedException)
            {
                readOnly = true;
            }
            Require(readOnly, "Navigation categories should expose a read-only snapshot.");
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
                    var noteItem = results.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
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
                    TextBox search = elements.OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "搜索条目");
                    TextBlock searchPlaceholder = elements.OfType<TextBlock>().Single(control => control.Text == "搜索 Note…");
                    Border searchShortcut = elements.OfType<Border>().Single(control => AutomationProperties.GetName(control) == "搜索快捷键提示");
                    Button clearSearch = elements.OfType<Button>().Single(control => AutomationProperties.GetName(control) == "清空搜索");

                    Require(documentTab.Child == title, "The active document tab should own the editable Note title.");
                    Require(bodySurface.Child == body, "The paper surface should be the direct host of the plain-text editor.");
                    Require(body.BorderThickness == new Thickness(0) && body.Padding.Left >= 20, "The plain-text canvas should stay borderless with a readable page margin.");
                    Require(Object.ReferenceEquals(body.Background, Application.Current.FindResource(ThemeResources.SurfaceBrushKey)), "The editor canvas should use the semantic paper surface.");
                    Require(saveState.Text == "已保存到本地" && saveState.Visibility == Visibility.Visible, "The editor header should keep local save truth visible beside the active Note.");
                    Require(searchPlaceholder.Visibility == Visibility.Visible, "The empty search field should expose a visible scope hint without replacing its automation name.");
                    Require(searchShortcut.Visibility == Visibility.Visible && clearSearch.Visibility == Visibility.Collapsed, "An empty search should show its keyboard hint without a redundant clear action.");

                    search.Text = "中文";
                    content.UpdateLayout();
                    Require(searchPlaceholder.Visibility == Visibility.Collapsed && searchShortcut.Visibility == Visibility.Collapsed, "An active query should remove empty-field guidance from the search surface.");
                    Require(clearSearch.Visibility == Visibility.Visible && clearSearch.IsEnabled, "An active query should expose an operable clear-search action.");
                    Require(AutomationProperties.GetHelpText(clearSearch).Contains("Esc"), "The clear-search action should expose its keyboard equivalent to assistive technology.");
                    string renderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "responsive-renders");
                    Directory.CreateDirectory(renderDirectory);
                    Render(content, 1080, 686, Path.Combine(renderDirectory, "search-active-default.png"));

                    clearSearch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    content.UpdateLayout();
                    Require(search.Text.Length == 0 && clearSearch.Visibility == Visibility.Collapsed, "The clear-search action should clear the query and remove itself.");
                    Require(searchPlaceholder.Visibility == Visibility.Visible && searchShortcut.Visibility == Visibility.Visible, "Clearing a query should restore the empty-search guidance.");

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
                    search.Text = "中文";
                    content.UpdateLayout();
                    Require(clearSearch.ActualWidth == 34 && clearSearch.ActualHeight == 34, "The clear-search action should keep a stable target at the supported minimum size.");
                    Render(content, 860, 506, Path.Combine(renderDirectory, "search-active-minimum.png"));
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void SearchResultsCompleteTheKeyboardFocusLoop()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Entries.Add(new Entry { Title = "第一条", Body = "第一条正文" });
                state.Entries.Add(new Entry { Title = "第二条", Body = "第二条正文" });
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = SystemParameters.VirtualScreenLeft - 2000,
                    Top = SystemParameters.VirtualScreenTop - 2000,
                    ShowInTaskbar = false
                };
                try
                {
                    window.Show();
                    window.Activate();
                    DrainDispatcher(TimeSpan.Zero);

                    List<DependencyObject> elements = Descendants(window).ToList();
                    TextBox search = elements.OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "搜索条目");
                    ListBox results = elements.OfType<ListBox>().Single(control => AutomationProperties.GetName(control) == "条目结果");
                    TextBox body = elements.OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "条目正文");
                    Require(AutomationProperties.GetHelpText(results).Contains("Enter") && AutomationProperties.GetHelpText(results).Contains("Esc"), "The result list should expose its keyboard actions to assistive technology.");

                    search.Text = "第二";
                    DrainDispatcher(TimeSpan.Zero);
                    Require(results.Items.Count == 1, "The keyboard-loop fixture should narrow to one result.");
                    results.SelectedIndex = 0;
                    Require(results.Focus(), "The result list should accept keyboard focus.");
                    DrainDispatcher(TimeSpan.Zero);

                    PresentationSource source = PresentationSource.FromVisual(results);
                    Require(source != null, "The visible test window should provide a keyboard presentation source.");
                    var enter = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent
                    };
                    results.RaiseEvent(enter);
                    DrainDispatcher(TimeSpan.Zero);
                    Require(enter.Handled && body.IsKeyboardFocusWithin, "Enter from a selected search result should move focus into the Note body.");

                    Require(results.Focus(), "The result list should regain focus before exercising Escape.");
                    var escape = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent
                    };
                    results.RaiseEvent(escape);
                    DrainDispatcher(TimeSpan.Zero);
                    Require(escape.Handled && search.Text.Length == 0 && search.IsKeyboardFocusWithin, "Escape from search results should clear the query and return focus to search.");
                    Require(viewModel.SelectedEntry != null && viewModel.SelectedEntry.Title == "第二条", "Clearing from the result list should preserve the selected Note context.");
                }
                finally
                {
                    window.Close();
                    window.Dispose();
                    DrainDispatcher(TimeSpan.Zero);
                }
            });
        }

        private static void EntryListVirtualizesLargeCollections()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                DateTime baseline = DateTime.UtcNow;
                for (int index = 0; index < 3000; index++)
                {
                    DateTime timestamp = baseline.AddSeconds(-index);
                    state.Entries.Add(new Entry
                    {
                        Title = "性能样本 " + index,
                        Body = "正文 " + index + " 中文搜索内容",
                        Category = index % 3 == 0 ? "工作" : "资料",
                        CreatedUtc = timestamp,
                        UpdatedUtc = timestamp
                    });
                }

                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();

                    ListBox results = Descendants(content).OfType<ListBox>().Single(control => AutomationProperties.GetName(control) == "条目结果");
                    Require(results.Items.Count == 3000, "Virtualization must retain the complete filtered result set.");
                    Require(VirtualizingStackPanel.GetIsVirtualizing(results), "The Note list should enable WPF UI virtualization.");
                    Require(VirtualizingStackPanel.GetVirtualizationMode(results) == VirtualizationMode.Recycling, "The Note list should recycle realized containers.");
                    Require(ScrollViewer.GetCanContentScroll(results), "Logical scrolling should remain enabled so virtualization is not disabled.");

                    int realized = Enumerable.Range(0, results.Items.Count).Count(index => results.ItemContainerGenerator.ContainerFromIndex(index) != null);
                    Require(realized > 0 && realized < 100, "A 3000-Note list should realize only viewport-near containers; actual=" + realized + ".");
                    Require(Object.ReferenceEquals(results.SelectedItem, viewModel.SelectedEntry), "Virtualized selection should stay bound to the selected Note data item.");

                    object stableItemsSource = results.ItemsSource;
                    Entry second = results.Items[1] as Entry;
                    results.SelectedIndex = 1;
                    TextBox title = Descendants(content).OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "条目标题");
                    TextBox body = Descendants(content).OfType<TextBox>().Single(control => AutomationProperties.GetName(control) == "条目正文");
                    Require(Object.ReferenceEquals(second, viewModel.SelectedEntry), "A narrow selection refresh should update the selected Note identity.");
                    Require(title.Text == "性能样本 1" && body.Text == "正文 1 中文搜索内容", "A narrow selection refresh should update both editor fields.");
                    Require(Object.ReferenceEquals(stableItemsSource, results.ItemsSource), "Pure selection should preserve the virtualized result source.");
                    results.SelectedIndex = 0;

                    var firstContainer = results.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                    Require(firstContainer != null && firstContainer.ContextMenu != null, "A realized virtualized Note should keep its context menu.");
                    Require(Object.ReferenceEquals(results.ItemContainerGenerator.ItemFromContainer(firstContainer), results.Items[0]), "A realized container should map back to the correct Note for selection and drag operations.");
                    Require(AutomationProperties.GetName(firstContainer) == "Note 性能样本 0", "A realized Note should expose its current UIA name.");
                    string renderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "responsive-renders");
                    Directory.CreateDirectory(renderDirectory);
                    Render(content, 1080, 686, Path.Combine(renderDirectory, "virtualized-dense-default.png"));

                    Entry last = results.Items[results.Items.Count - 1] as Entry;
                    results.ScrollIntoView(last);
                    content.UpdateLayout();
                    var lastContainer = results.ItemContainerGenerator.ContainerFromIndex(results.Items.Count - 1) as ListBoxItem;
                    Require(lastContainer != null, "Scrolling should realize the requested final Note.");
                    Require(AutomationProperties.GetName(lastContainer) == "Note 性能样本 2999", "A recycled container must replace the previous Note's UIA name.");
                    List<string> menuHeaders = lastContainer.ContextMenu.Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();
                    Require(menuHeaders.Contains("复制正文") && menuHeaders.Contains("复制 Note ID") && menuHeaders.Contains("移动到分类") && menuHeaders.Contains("移到回收站"), "Recycled Note containers should retain the complete action menu.");

                    results.ScrollIntoView(results.Items[0]);
                    content.InvalidateMeasure();
                    content.Measure(new Size(860, 506));
                    content.Arrange(new Rect(0, 0, 860, 506));
                    content.UpdateLayout();
                    Render(content, 860, 506, Path.Combine(renderDirectory, "virtualized-dense-minimum.png"));

                    title.Text = "性能样本 已编辑";
                    body.Text = "正文预览 已编辑";
                    DrainDispatcher(TimeSpan.FromMilliseconds(160));
                    content.UpdateLayout();
                    var editedContainer = results.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                    Require(editedContainer != null && AutomationProperties.GetName(editedContainer) == "Note 性能样本 已编辑", "Deferred text refresh should update the selected Note title in the result list.");
                    var editedRow = editedContainer.Content as Grid;
                    Require(editedRow != null && editedRow.Children.OfType<TextBlock>().Any(control => control.Text == "正文预览 已编辑"), "Deferred text refresh should update the selected Note preview in the result list.");
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void EntryContextMenusAreSharedAndRetargeted()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                for (int index = 0; index < 100; index++)
                {
                    state.Categories.Add("菜单分类 " + index.ToString("D3"));
                }
                DateTime baseline = DateTime.UtcNow;
                for (int index = 0; index < 200; index++)
                {
                    state.Entries.Add(new Entry
                    {
                        Title = "菜单目标 " + index,
                        Body = "共享菜单正文 " + index,
                        Category = state.Categories[index % state.Categories.Count],
                        IsFavorite = index == 0,
                        CreatedUtc = baseline.AddSeconds(-index),
                        UpdatedUtc = baseline.AddSeconds(-index)
                    });
                }
                for (int index = 0; index < 8; index++)
                {
                    state.Entries.Add(new Entry
                    {
                        Title = "已删除菜单目标 " + index,
                        Body = "已删除共享菜单正文 " + index,
                        Category = state.Categories[index],
                        IsDeleted = true,
                        DeletedUtc = baseline.AddMinutes(-index - 1),
                        CreatedUtc = baseline.AddMinutes(-index - 1),
                        UpdatedUtc = baseline.AddMinutes(-index - 1)
                    });
                }

                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();

                    ListBox results = Descendants(content).OfType<ListBox>().Single(control => AutomationProperties.GetName(control) == "条目结果");
                    List<ListBoxItem> containers = Enumerable.Range(0, results.Items.Count)
                        .Select(index => results.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
                        .Where(container => container != null)
                        .ToList();
                    Require(containers.Count > 1, "The shared-menu test requires more than one realized Note row.");
                    ContextMenu sharedMenu = containers[0].ContextMenu;
                    Require(sharedMenu != null && containers.All(container => Object.ReferenceEquals(container.ContextMenu, sharedMenu)), "Active Note rows should share one command tree instead of prebuilding one tree per realized container.");

                    Entry first = results.ItemContainerGenerator.ItemFromContainer(containers[0]) as Entry;
                    Entry second = results.ItemContainerGenerator.ItemFromContainer(containers[1]) as Entry;
                    Require(first != null && second != null && !Object.ReferenceEquals(first, second), "Each realized container should retain its own Entry identity while sharing commands.");

                    RaiseContextMenuOpening(containers[0]);
                    FieldInfo preparedEntryField = sharedMenu.GetType().GetField("_entry", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(preparedEntryField != null && Object.ReferenceEquals(preparedEntryField.GetValue(sharedMenu), first), "Opening the shared menu should retain only its current target while the menu is active.");
                    sharedMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));
                    Require(preparedEntryField.GetValue(sharedMenu) == null, "Closing the shared menu should release its prepared Entry reference.");
                    RaiseContextMenuOpening(containers[0]);
                    List<string> topHeaders = sharedMenu.Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();
                    Require(topHeaders.SequenceEqual(new[] { "复制正文", "复制 Note ID", "复制为 JSON", "取消收藏置顶", "打开置顶小窗", "移动到分类", "移到回收站" }), "The shared active menu should preserve the exact command order and current favorite label.");
                    MenuItem move = sharedMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "移动到分类");
                    MenuItem firstChecked = move.Items.OfType<MenuItem>().Single(item => item.IsChecked);
                    Require((string)firstChecked.Header == first.Category, "The shared menu should check the first placement target's category.");

                    RaiseContextMenuOpening(containers[1]);
                    MenuItem favorite = sharedMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "收藏置顶");
                    Require(favorite != null, "Opening the shared menu for a non-favorite Note should refresh the favorite label.");
                    Require(Object.ReferenceEquals(results.SelectedItem, second) && Object.ReferenceEquals(viewModel.SelectedEntry, second), "Opening a context menu from an unselected row should synchronize the visible and view-model selection.");
                    MenuItem secondChecked = move.Items.OfType<MenuItem>().Single(item => item.IsChecked);
                    Require((string)secondChecked.Header == second.Category, "Opening the shared menu for another row should replace the checked category without stale state.");

                    viewModel.SelectEntry(first);
                    favorite.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, favorite));
                    Require(first.IsFavorite && second.IsFavorite, "A shared favorite command should retain its prepared target even if selection changes before invocation.");
                    Require(Object.ReferenceEquals(viewModel.SelectedEntry, second), "Invoking a prepared shared command should restore selection to its exact target.");

                    results.ScrollIntoView(second);
                    content.UpdateLayout();
                    var secondContainer = results.ItemContainerGenerator.ContainerFromItem(second) as ListBoxItem;
                    Require(secondContainer != null, "The retargeted Note should remain realizable after its favorite state changes.");
                    RaiseContextMenuOpening(secondContainer);

                    string destination = state.Categories[(state.Categories.IndexOf(second.Category) + 1) % state.Categories.Count];
                    MenuItem destinationItem = move.Items.OfType<MenuItem>().Single(item => String.Equals(item.Header as string, destination, StringComparison.Ordinal));
                    destinationItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, destinationItem));
                    Require(String.Equals(second.Category, destination, StringComparison.Ordinal), "A shared move command should target the exact row that opened the menu, not the row that first created it.");
                    Require(!String.Equals(first.Category, destination, StringComparison.Ordinal), "Retargeting the shared menu should not move a different selected or previously prepared Note.");

                    results.ScrollIntoView(second);
                    content.UpdateLayout();
                    secondContainer = results.ItemContainerGenerator.ContainerFromItem(second) as ListBoxItem;
                    Require(secondContainer != null, "The moved Note should remain realizable before exercising its destructive menu command.");
                    RaiseContextMenuOpening(secondContainer);
                    viewModel.SelectEntry(first);
                    MenuItem softDelete = sharedMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "移到回收站");
                    softDelete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, softDelete));
                    Require(second.IsDeleted && !first.IsDeleted, "A shared destructive command should delete only its prepared target after selection changes.");

                    viewModel.SelectView(SmartView.Trash);
                    content.UpdateLayout();
                    List<ListBoxItem> deletedContainers = Enumerable.Range(0, results.Items.Count)
                        .Select(index => results.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
                        .Where(container => container != null)
                        .ToList();
                    Require(deletedContainers.Count > 1, "The deleted-menu target test requires multiple realized deleted Notes.");
                    ContextMenu deletedMenu = deletedContainers[0].ContextMenu;
                    Require(deletedMenu != null && deletedContainers.All(container => Object.ReferenceEquals(container.ContextMenu, deletedMenu)), "Deleted Note rows should share their own command tree.");
                    Entry deletedOther = results.ItemContainerGenerator.ItemFromContainer(deletedContainers[0]) as Entry;
                    Entry deletedTarget = results.ItemContainerGenerator.ItemFromContainer(deletedContainers[1]) as Entry;
                    Require(deletedOther != null && deletedTarget != null && !Object.ReferenceEquals(deletedOther, deletedTarget), "Deleted rows should retain distinct Entry identities.");
                    RaiseContextMenuOpening(deletedContainers[1]);
                    viewModel.SelectEntry(deletedOther);
                    MenuItem restore = deletedMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "还原 Note");
                    restore.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, restore));
                    Require(!deletedTarget.IsDeleted && deletedOther.IsDeleted, "A shared restore command should affect only its prepared deleted Note after selection changes.");
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void EntryContextMenuSharingAdaptsAtViewportThreshold()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                for (int index = 0; index < 20; index++)
                {
                    state.Categories.Add("阈值分类 " + index.ToString("D2"));
                }
                DateTime baseline = DateTime.UtcNow;
                for (int index = 0; index < 6; index++)
                {
                    state.Entries.Add(new Entry
                    {
                        Title = "直接菜单 " + index,
                        Category = state.Categories[index],
                        CreatedUtc = baseline.AddSeconds(-index),
                        UpdatedUtc = baseline.AddSeconds(-index)
                    });
                }

                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();
                    ListBox results = Descendants(content).OfType<ListBox>().Single(control => AutomationProperties.GetName(control) == "条目结果");
                    List<ListBoxItem> six = Enumerable.Range(0, results.Items.Count)
                        .Select(index => results.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
                        .Where(container => container != null)
                        .ToList();
                    Require(six.Count == 6 && six.Select(container => container.ContextMenu).Distinct().Count() == 6, "One to six visible Notes should keep direct per-row menus and avoid shared-menu bookkeeping.");

                    Entry directOther = results.ItemContainerGenerator.ItemFromContainer(six[0]) as Entry;
                    Entry directTarget = results.ItemContainerGenerator.ItemFromContainer(six[1]) as Entry;
                    ContextMenu directMenu = six[1].ContextMenu;
                    Require(directOther != null && directTarget != null && directMenu != null, "The direct-menu target test requires two distinct realized Notes.");
                    bool directOtherFavorite = directOther.IsFavorite;
                    bool directTargetFavorite = directTarget.IsFavorite;
                    RaiseContextMenuOpening(six[1]);
                    Require(Object.ReferenceEquals(results.SelectedItem, directTarget) && Object.ReferenceEquals(directMenu.Tag, directTarget), "Opening a direct menu should synchronize selection and freeze its exact row target.");
                    RaiseContextMenuClosing(six[1]);
                    Require(directMenu.Tag == null, "Closing a direct menu without invoking a command should release its prepared Entry target.");
                    RaiseContextMenuOpening(six[1]);
                    viewModel.SelectEntry(directOther);
                    MenuItem directFavorite = directMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "收藏置顶");
                    directFavorite.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, directFavorite));
                    Require(directTarget.IsFavorite != directTargetFavorite && directOther.IsFavorite == directOtherFavorite, "A direct favorite command should retain its opened row target after selection changes.");
                    Require(directMenu.Tag == null, "Invoking a direct menu command should release its prepared Entry target.");

                    viewModel.CreateEntry();
                    content.UpdateLayout();
                    List<ListBoxItem> seven = Enumerable.Range(0, results.Items.Count)
                        .Select(index => results.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
                        .Where(container => container != null)
                        .ToList();
                    Require(seven.Count == 7 && seven.Select(container => container.ContextMenu).Distinct().Count() == 1, "The seventh Note should switch all realized rows to one shared command tree.");

                    viewModel.SoftDeleteSelected();
                    content.UpdateLayout();
                    six = Enumerable.Range(0, results.Items.Count)
                        .Select(index => results.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
                        .Where(container => container != null)
                        .ToList();
                    Require(six.Count == 6 && six.Select(container => container.ContextMenu).Distinct().Count() == 6, "Shrinking back to six Notes should restore direct menus without retaining shared targets on visible rows.");

                    Entry returnedOther = results.ItemContainerGenerator.ItemFromContainer(six[0]) as Entry;
                    Entry returnedTarget = results.ItemContainerGenerator.ItemFromContainer(six[1]) as Entry;
                    ContextMenu returnedMenu = six[1].ContextMenu;
                    Require(returnedOther != null && returnedTarget != null && returnedMenu != null, "The post-threshold direct-menu test requires two distinct Notes.");
                    RaiseContextMenuOpening(six[1]);
                    viewModel.SelectEntry(returnedOther);
                    MenuItem directDelete = returnedMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "移到回收站");
                    directDelete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, directDelete));
                    Require(returnedTarget.IsDeleted && !returnedOther.IsDeleted, "A direct destructive command should retain its exact target after a 6-to-7-to-6 transition and selection change.");
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void StatusFeedbackExposesScopedLiveRegions()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Entries.Add(new Entry { Title = "状态反馈", Body = "正文" });
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    TextBlock status = Descendants(window).OfType<TextBlock>().Single(control => (AutomationProperties.GetName(control) ?? String.Empty).StartsWith("应用状态：", StringComparison.Ordinal));

                    viewModel.ReportStatus("已复制当前 Note。", false);
                    Require(status.Text == "已复制当前 Note。" && AutomationProperties.GetName(status).Contains("已复制当前 Note"), "The live region should expose the current actionable status text through UI Automation.");
                    Require(AutomationProperties.GetLiveSetting(status) == AutomationLiveSetting.Polite, "Successful action feedback should use a non-interruptive live setting.");

                    viewModel.ReportStatus("复制失败：剪贴板不可用。", true);
                    Require(status.Text.StartsWith("复制失败", StringComparison.Ordinal) && AutomationProperties.GetName(status).Contains("复制失败"), "The live region should expose the current failure text through UI Automation.");
                    Require(AutomationProperties.GetLiveSetting(status) == AutomationLiveSetting.Assertive, "Actionable failures should use an assertive live setting.");

                    viewModel.UpdateSelectedBody("自动保存状态仍保持可见但不主动播报");
                    Require(status.Text == "尚未保存" && !viewModel.StatusShouldAnnounce, "Routine autosave state should remain visible without entering the live-announcement queue.");
                    Require(AutomationProperties.GetLiveSetting(status) == AutomationLiveSetting.Polite, "A non-error visual status should return the live region to polite priority.");
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
                    var noteItem = results.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
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

        private static void CategorySidebarReordersExistingRows()
        {
            EnsureApplication();
            var sidebar = new CategorySidebar();
            IList<string> categories = new List<string> { "Codex", "资料", "归档" };
            var counts = new Dictionary<string, int> { { "Codex", 1 }, { "资料", 2 }, { "归档", 3 } };
            sidebar.Refresh(categories, "资料", counts);
            ListBox list = Descendants(sidebar).OfType<ListBox>().Single();
            var originalRows = list.Items.Cast<ListBoxItem>().ToDictionary(item => (string)item.Tag);
            int collectionChanges = 0;
            ((INotifyCollectionChanged)list.Items).CollectionChanged += delegate { collectionChanges++; };

            string renamedCategory = null;
            string deletedCategory = null;
            sidebar.RenameRequested += delegate(object sender, CategoryNameEventArgs eventArgs) { renamedCategory = eventArgs.Category; };
            sidebar.DeleteRequested += delegate(object sender, CategoryNameEventArgs eventArgs) { deletedCategory = eventArgs.Category; };
            IList<string> reordered = new List<string> { "归档", "Codex", "资料" };
            counts["归档"] = 30;
            sidebar.Refresh(reordered, "Codex", counts);

            Require(list.Items.Cast<ListBoxItem>().Select(item => item.Tag as string).SequenceEqual(reordered), "A reordered sidebar should expose the exact requested category order.");
            Require(collectionChanges == 2, "Moving one category should require one remove/insert pair instead of a full collection reset.");
            Require(reordered.All(category => Object.ReferenceEquals(originalRows[category], list.Items.Cast<ListBoxItem>().Single(item => (string)item.Tag == category))), "A pure category reorder should move existing rows instead of recreating them.");
            Require(Object.ReferenceEquals(list.SelectedItem, originalRows["Codex"]), "A reordered sidebar should retain the requested selection on its reused row.");

            var archiveRow = originalRows["归档"];
            var archiveContent = (Grid)archiveRow.Content;
            Require(((TextBlock)archiveContent.Children[1]).Text == "30", "A reused reordered row should still update its count.");
            Require(AutomationProperties.GetName(archiveRow) == "归档，30 条 Note", "A reused reordered row should keep its accessible name synchronized.");
            Require((string)archiveRow.Tag == "归档" && archiveRow.ToolTip.ToString().Contains("归档"), "A reused row should keep the category identity used by drag and drop.");
            ContextMenu sharedMenu = archiveRow.ContextMenu;
            Require(sharedMenu != null && reordered.All(category => Object.ReferenceEquals(sharedMenu, originalRows[category].ContextMenu)), "Category rows should share one context menu instead of allocating identical command trees per row.");

            var rightClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent
            };
            archiveRow.RaiseEvent(rightClick);
            Require(Object.ReferenceEquals(list.SelectedItem, archiveRow), "Right-clicking a row should still select it before the shared context menu opens.");

            OpenContextMenu(sharedMenu);
            sharedMenu.PlacementTarget = archiveRow;
            ((MenuItem)sharedMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(renamedCategory == "归档", "A reused row's context menu should still target its own category after reordering.");
            sharedMenu.PlacementTarget = originalRows["资料"];
            ((MenuItem)sharedMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(deletedCategory == "资料", "A shared context menu should resolve each command against its current placement target.");

            collectionChanges = 0;
            sidebar.Refresh(categories, "资料", counts);
            Require(collectionChanges == 2 && list.Items.Cast<ListBoxItem>().Select(item => item.Tag as string).SequenceEqual(categories), "Moving the leading category back to the end should also use one remove/insert pair.");
            Require(Object.ReferenceEquals(list.SelectedItem, originalRows["资料"]), "Reverse reordering should preserve the selected row identity.");

            sidebar.Refresh(new List<string> { "归档", "CODEX", "资料" }, "CODEX", counts);
            var renamedRow = (ListBoxItem)list.Items[1];
            Require(!Object.ReferenceEquals(renamedRow, originalRows["Codex"]), "A display-name change should replace the row so visible and interactive identity cannot retain the old spelling.");
            Require((string)renamedRow.Tag == "CODEX" && ((TextBlock)((Grid)renamedRow.Content).Children[0]).Text == "CODEX", "A display-name change should update the visible and interactive category identity.");

            IList<string> replaced = new List<string> { "CODEX", "资料", "新建" };
            sidebar.Refresh(replaced, "新建", counts);
            Require(list.Items.Cast<ListBoxItem>().Select(item => item.Tag as string).SequenceEqual(replaced), "Adding and removing categories in one refresh should preserve the requested order.");
            Require(Object.ReferenceEquals(list.Items[0], renamedRow) && Object.ReferenceEquals(list.Items[1], originalRows["资料"]), "Topology changes should retain every still-valid category row.");
            Require(!list.Items.Contains(originalRows["归档"]) && (string)((ListBoxItem)list.SelectedItem).Tag == "新建", "Removed rows should leave the sidebar and newly created rows should remain selectable.");
            Require(Object.ReferenceEquals(sharedMenu, renamedRow.ContextMenu) && Object.ReferenceEquals(sharedMenu, ((ListBoxItem)list.Items[2]).ContextMenu), "Replacement and newly added rows should reuse the sidebar context menu.");
            renamedCategory = null;
            sharedMenu.PlacementTarget = (ListBoxItem)list.Items[2];
            ((MenuItem)sharedMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(renamedCategory == "新建", "A newly added row's context menu should target the new category.");
        }

        private static void CategorySidebarVirtualizesScrollableCollections()
        {
            EnsureApplication();
            var categories = Enumerable.Range(0, 100).Select(index => "分类 " + index.ToString("D3")).ToList();
            categories[categories.Count - 1] = "这是一个用于验证中文截断、工具提示和无障碍名称的很长分类名称";
            var counts = categories.ToDictionary(category => category, category => 10, StringComparer.InvariantCultureIgnoreCase);
            var sidebar = new CategorySidebar();
            sidebar.Refresh(categories, categories[50], counts);
            sidebar.Measure(new Size(240, 320));
            sidebar.Arrange(new Rect(0, 0, 240, 320));
            sidebar.UpdateLayout();

            ListBox list = Descendants(sidebar).OfType<ListBox>().Single();
            Require(list.Items.Count == categories.Count, "A virtualized sidebar must retain every category data item.");
            Require(list.ItemsSource != null, "A scrollable category collection should use data items so WPF can generate containers on demand.");
            Require(VirtualizingStackPanel.GetIsVirtualizing(list), "A scrollable category collection should enable WPF UI virtualization.");
            Require(VirtualizingStackPanel.GetVirtualizationMode(list) == VirtualizationMode.Recycling, "Scrollable category rows should recycle realized containers.");
            Require(ScrollViewer.GetCanContentScroll(list), "Logical scrolling should remain enabled for category virtualization.");
            int realized = Enumerable.Range(0, list.Items.Count).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) != null);
            Require(realized > 0 && realized < 20, "A 100-category sidebar should realize only viewport-near rows; actual=" + realized + ".");
            Require(list.SelectedItem != null && list.SelectedItem.ToString() == categories[50], "Virtualized selection should preserve the exact selected category.");

            object stableItemsSource = list.ItemsSource;
            object firstDataItem = list.Items[0];
            object selectedDataItem = list.SelectedItem;
            list.ScrollIntoView(firstDataItem);
            sidebar.UpdateLayout();
            var firstContainer = list.ItemContainerGenerator.ContainerFromItem(firstDataItem) as ListBoxItem;
            Require(firstContainer != null && (string)firstContainer.Tag == categories[0], "A realized category container should map back to the current drag/drop identity.");
            Require(AutomationProperties.GetName(firstContainer) == categories[0] + "，10 条 Note", "A realized category should expose its current count through UI Automation.");
            Require(firstContainer.ToolTip.ToString().Contains(categories[0]), "A realized category should retain its full drag/drop tooltip.");
            ContextMenu sharedMenu = firstContainer.ContextMenu;
            Require(sharedMenu != null, "A realized virtualized category should retain the shared context menu.");

            counts[categories[0]] = 11;
            sidebar.Refresh(categories, categories[75], counts);
            Require(Object.ReferenceEquals(stableItemsSource, list.ItemsSource), "Count and selection refreshes should preserve the virtual category source.");
            Require(Object.ReferenceEquals(firstDataItem, list.Items[0]), "Count and selection refreshes should preserve category data-item identity.");
            Require(Object.ReferenceEquals(firstContainer, list.ItemContainerGenerator.ContainerFromItem(firstDataItem)), "A visible count change should update its existing realized container.");
            Require(AutomationProperties.GetName(firstContainer) == categories[0] + "，11 条 Note", "A visible count change should refresh the recycled container's UIA name.");
            Require(list.SelectedItem.ToString() == categories[75], "A selection-only refresh should select the requested virtual data item.");
            selectedDataItem = list.SelectedItem;

            int collectionChanges = 0;
            ((INotifyCollectionChanged)list.Items).CollectionChanged += delegate { collectionChanges++; };
            IList<string> reordered = categories.Skip(1).Concat(categories.Take(1)).ToList();
            sidebar.Refresh(reordered, categories[75], counts);
            Require(collectionChanges == 1, "Moving one virtual category should emit one collection move instead of remove/insert churn.");
            Require(list.Items.Cast<object>().Select(item => item.ToString()).SequenceEqual(reordered), "A virtual category move should preserve the requested order.");
            Require(Object.ReferenceEquals(firstDataItem, list.Items[list.Items.Count - 1]), "A virtual category move should preserve the moved data-item identity.");
            Require(Object.ReferenceEquals(selectedDataItem, list.SelectedItem), "A virtual category move should preserve selected data-item identity.");

            collectionChanges = 0;
            sidebar.Refresh(categories, categories[75], counts);
            Require(collectionChanges == 1 && Object.ReferenceEquals(firstDataItem, list.Items[0]), "The reverse virtual category move should also use one Move notification and preserve identity.");

            string selectedCategory = null;
            string renamedCategory = null;
            EntryCategoryMoveEventArgs movedEntry = null;
            CategoryReorderEventArgs movedCategory = null;
            sidebar.CategorySelected += delegate(object sender, CategoryNameEventArgs eventArgs) { selectedCategory = eventArgs.Category; };
            sidebar.RenameRequested += delegate(object sender, CategoryNameEventArgs eventArgs) { renamedCategory = eventArgs.Category; };
            sidebar.EntryMoveRequested += delegate(object sender, EntryCategoryMoveEventArgs eventArgs) { movedEntry = eventArgs; };
            sidebar.ReorderRequested += delegate(object sender, CategoryReorderEventArgs eventArgs) { movedCategory = eventArgs; };

            object longDataItem = list.Items[list.Items.Count - 1];
            list.ScrollIntoView(longDataItem);
            sidebar.UpdateLayout();
            var longContainer = list.ItemContainerGenerator.ContainerFromItem(longDataItem) as ListBoxItem;
            Require(longContainer != null && (string)longContainer.Tag == categories[categories.Count - 1], "Scrolling should realize the final long Chinese category.");
            Require(Object.ReferenceEquals(sharedMenu, longContainer.ContextMenu), "Recycled virtual category containers should reuse the sidebar context menu.");
            Require(AutomationProperties.GetName(longContainer) == categories[categories.Count - 1] + "，10 条 Note", "A recycled category container must replace its UIA name.");
            Require(longContainer.ToolTip.ToString().Contains(categories[categories.Count - 1]), "A recycled category container must replace its full tooltip.");
            var longRow = longContainer.Content as Grid;
            Require(longRow != null && ((TextBlock)longRow.Children[0]).Text == categories[categories.Count - 1] && ((TextBlock)longRow.Children[0]).TextTrimming == TextTrimming.CharacterEllipsis, "A long category should retain exact text and visual ellipsis behavior.");

            var rightClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent
            };
            longContainer.RaiseEvent(rightClick);
            Require(Object.ReferenceEquals(list.SelectedItem, longDataItem) && selectedCategory == categories[categories.Count - 1], "Right-clicking a recycled category should select and report its current data item.");
            OpenContextMenu(sharedMenu);
            sharedMenu.PlacementTarget = longContainer;
            ((MenuItem)sharedMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(renamedCategory == categories[categories.Count - 1], "A recycled category's shared menu should resolve its current placement target.");

            Guid entryId = Guid.NewGuid();
            var entryDrop = CreateDragEventArgs(
                new DataObject(CategorySidebar.EntryDragFormat, entryId.ToString("D")),
                longContainer,
                new Point(2, 2));
            entryDrop.RoutedEvent = DragDrop.DropEvent;
            longContainer.RaiseEvent(entryDrop);
            Require(movedEntry != null && movedEntry.EntryId == entryId && movedEntry.Category == categories[categories.Count - 1], "Dropping a Note on a recycled category should retain the exact target identity.");

            var categoryDrop = CreateDragEventArgs(
                new DataObject(CategorySidebar.CategoryDragFormat, categories[0]),
                longContainer,
                new Point(2, Math.Max(1, longContainer.ActualHeight - 1)));
            categoryDrop.RoutedEvent = DragDrop.DropEvent;
            longContainer.RaiseEvent(categoryDrop);
            Require(movedCategory != null && movedCategory.Category == categories[0] && movedCategory.TargetCategory == categories[categories.Count - 1] && movedCategory.InsertAfter, "Dropping a category on a recycled row should preserve source, target and insertion side.");

            var emptySidebar = new CategorySidebar();
            emptySidebar.Refresh(new List<string>(), null, counts);
            Require(!Descendants(emptySidebar).OfType<ListBox>().Any(), "An empty category sidebar should avoid constructing an invisible list.");
            emptySidebar.Refresh(new List<string> { categories[0] }, categories[0], counts);
            ListBox materializedList = Descendants(emptySidebar).OfType<ListBox>().Single();
            Require(materializedList.ItemsSource == null && (string)((ListBoxItem)materializedList.SelectedItem).Tag == categories[0], "The first real category should materialize the list and preserve selection.");

            var thresholdSidebar = new CategorySidebar();
            IList<string> six = categories.Take(6).ToList();
            IList<string> seven = categories.Take(7).ToList();
            thresholdSidebar.Refresh(six, six[5], counts);
            ListBox thresholdList = Descendants(thresholdSidebar).OfType<ListBox>().Single();
            Require(thresholdList.ItemsSource == null && thresholdList.Items.Cast<object>().All(item => item is ListBoxItem), "A category set that fits without scrolling should retain the lightweight direct-row path.");
            thresholdSidebar.Refresh(seven, seven[6], counts);
            Require(thresholdList.ItemsSource != null && thresholdList.SelectedItem.ToString() == seven[6], "Crossing the viewport limit should switch to virtual data items without losing selection.");
            thresholdSidebar.Refresh(six, six[5], counts);
            Require(thresholdList.ItemsSource == null && (string)((ListBoxItem)thresholdList.SelectedItem).Tag == six[5], "Shrinking below the viewport limit should restore direct rows without losing selection.");

            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                foreach (string category in categories)
                {
                    state.Categories.Add(category);
                }
                DateTime now = DateTime.UtcNow;
                for (int index = 0; index < 500; index++)
                {
                    state.Entries.Add(new Entry
                    {
                        Title = "分类虚拟化样本 " + index,
                        Body = "正文 " + index,
                        Category = categories[index % categories.Count],
                        CreatedUtc = now.AddSeconds(-index),
                        UpdatedUtc = now.AddSeconds(-index)
                    });
                }
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1080, 686));
                    content.Arrange(new Rect(0, 0, 1080, 686));
                    content.UpdateLayout();
                    ListBox renderedList = Descendants(content).OfType<ListBox>().Single(control => AutomationProperties.GetName(control) == "自定义分类列表");
                    int rendered = Enumerable.Range(0, renderedList.Items.Count).Count(index => renderedList.ItemContainerGenerator.ContainerFromIndex(index) != null);
                    Require(rendered > 0 && rendered < 20, "The real default window should keep the 100-category sidebar virtualized; actual=" + rendered + ".");
                    string renderDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "responsive-renders");
                    Directory.CreateDirectory(renderDirectory);
                    Render(content, 1080, 686, Path.Combine(renderDirectory, "virtualized-categories-default.png"));

                    content.InvalidateMeasure();
                    content.Measure(new Size(860, 506));
                    content.Arrange(new Rect(0, 0, 860, 506));
                    content.UpdateLayout();
                    Render(content, 860, 506, Path.Combine(renderDirectory, "virtualized-categories-minimum.png"));
                }
                finally
                {
                    window.Dispose();
                }
            });
        }

        private static void CategoryPickerPreservesStableItems()
        {
            EnsureApplication();
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("工作");
                state.Categories.Add("资料");
                var first = new Entry { Title = "第一条", Category = "工作", UpdatedUtc = DateTime.UtcNow };
                var second = new Entry { Title = "第二条", Category = "资料", UpdatedUtc = first.UpdatedUtc.AddMinutes(-1) };
                state.Entries.Add(first);
                state.Entries.Add(second);
                var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher);
                var window = new MainWindow(viewModel);
                try
                {
                    ComboBox picker = Descendants(window).OfType<ComboBox>().Single(control => AutomationProperties.GetName(control) == "条目分类");
                    int collectionChanges = 0;
                    ((INotifyCollectionChanged)picker.Items).CollectionChanged += delegate { collectionChanges++; };

                    viewModel.SelectEntry(second);
                    Require(collectionChanges == 0, "Selecting another Note should preserve unchanged category picker items.");
                    Require((picker.SelectedItem as string) == "资料", "A stable category picker should still follow the selected Note.");

                    string error;
                    Require(viewModel.RenameCategory("资料", "参考", out error), error);
                    Require(collectionChanges > 0, "A real category rename should rebuild the category picker items.");
                    Require(picker.Items.Cast<string>().SequenceEqual(new[] { "未分类", "工作", "参考" }), "The rebuilt category picker should expose the current ordered categories.");
                    Require((picker.SelectedItem as string) == "参考", "The rebuilt category picker should preserve the selected Note's renamed category.");
                }
                finally
                {
                    window.Dispose();
                }
            });
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

        private static void RaiseContextMenuOpening(FrameworkElement source)
        {
            ConstructorInfo constructor = typeof(ContextMenuEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object), typeof(bool) },
                null);
            Require(constructor != null, "WPF should expose the non-public ContextMenuEventArgs constructor used by the routed-event test.");
            var eventArgs = (ContextMenuEventArgs)constructor.Invoke(new object[] { source, true });
            eventArgs.RoutedEvent = ContextMenuService.ContextMenuOpeningEvent;
            source.RaiseEvent(eventArgs);
        }

        private static void RaiseContextMenuClosing(FrameworkElement source)
        {
            ConstructorInfo constructor = typeof(ContextMenuEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object), typeof(bool) },
                null);
            Require(constructor != null, "WPF should expose the non-public ContextMenuEventArgs constructor used by the routed-event test.");
            var eventArgs = (ContextMenuEventArgs)constructor.Invoke(new object[] { source, false });
            eventArgs.RoutedEvent = ContextMenuService.ContextMenuClosingEvent;
            source.RaiseEvent(eventArgs);
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

        private static DragEventArgs CreateDragEventArgs(IDataObject data, DependencyObject target, Point point)
        {
            var constructor = typeof(DragEventArgs).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point) },
                null);
            Require(constructor != null, "WPF should expose the internal DragEventArgs constructor used by the routed drop test.");
            return (DragEventArgs)constructor.Invoke(new object[] { data, DragDropKeyStates.None, DragDropEffects.Move, target, point });
        }

        private static void OpenContextMenu(ContextMenu menu)
        {
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Require(menu.Items.Count == 2, "Opening the shared category menu should materialize both commands.");
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
