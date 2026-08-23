using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SeerNote.Agent;
using SeerNote.Domain;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    public sealed class MainWindow : Window, IDisposable
    {
        private readonly MainViewModel _viewModel;
        private readonly StickyWindowManager _stickyWindows;
        private readonly DispatcherTimer _resultsRefreshTimer;
        private readonly Dictionary<SmartView, Button> _viewButtons = new Dictionary<SmartView, Button>();
        private readonly Dictionary<SmartView, TextBlock> _viewCounts = new Dictionary<SmartView, TextBlock>();
        private TextBox _searchBox;
        private TextBlock _searchPlaceholder;
        private Border _searchShortcut;
        private Button _clearSearchButton;
        private TextBlock _resultCount;
        private ListBox _entryList;
        private StackPanel _emptyResults;
        private TextBlock _emptyResultsText;
        private Button _emptyCreateButton;
        private Button _emptyTrashButton;
        private CategorySidebar _categorySidebar;
        private Grid _mainGrid;
        private ColumnDefinition _sidebarColumn;
        private ColumnDefinition _listColumn;
        private ColumnDefinition _editorColumn;
        private Grid _editorContent;
        private Border _editorEmpty;
        private TextBlock _editorEmptyTitle;
        private TextBlock _editorEmptyHint;
        private TextBox _titleBox;
        private TextBox _bodyBox;
        private ComboBox _categoryBox;
        private Button _favoriteButton;
        private Button _copyButton;
        private Button _stickyButton;
        private Button _deleteButton;
        private Button _restoreButton;
        private Button _permanentDeleteButton;
        private TextBlock _documentStateText;
        private TextBlock _statusText;
        private Button _retrySaveButton;
        private bool _refreshing;
        private bool _editing;
        private bool _initializingBounds;
        private bool _disposed;
        private int _lastAnnouncedStatusRevision = -1;
        private NavigationSnapshot _lastNavigationSnapshot;
        private NavigationSnapshot _lastCategoryPickerSnapshot;
        private SmartView _lastNavigationView;
        private string _lastNavigationCategory;
        private Point _entryDragStart;
        private Entry _entryDragEntry;

        public MainWindow(MainViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _stickyWindows = new StickyWindowManager(OnStickyEntryChanged);
            _resultsRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _resultsRefreshTimer.Tick += ResultsRefreshTimerOnTick;

            Title = "SeerNote · 本地 Note";
            MinWidth = MainWindowLayoutCalculator.MinimumWindowWidth;
            MinHeight = MainWindowLayoutCalculator.MinimumWindowHeight;
            Width = MainWindowLayoutCalculator.BaselineWindowWidth;
            Height = MainWindowLayoutCalculator.BaselineWindowHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Background = Brush(ThemeResources.CanvasBrushKey);
            FontFamily = (FontFamily)FindResource(ThemeResources.UiFontFamilyKey);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            SnapsToDevicePixels = true;
            AutomationProperties.SetName(this, "SeerNote 主窗口");

            _initializingBounds = true;
            ApplyStoredBounds();
            Content = BuildWindowContent();
            _initializingBounds = false;

            _viewModel.ContentChanged += ViewModelOnContentChanged;
            _viewModel.SelectedEntryChanged += ViewModelOnSelectedEntryChanged;
            _viewModel.StatusChanged += ViewModelOnStatusChanged;
            PreviewKeyDown += MainWindowOnPreviewKeyDown;
            LocationChanged += WindowBoundsOnChanged;
            SizeChanged += WindowBoundsOnChanged;
            Loaded += MainWindowOnLoaded;

            RefreshAll();
        }

        public event EventHandler ExitRequested;
        public event EventHandler ThemeChanged;

        public MainViewModel ViewModel
        {
            get { return _viewModel; }
        }

        public void ShowAndFocus(bool focusSearch)
        {
            if (!IsVisible)
            {
                Show();
            }
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            if (focusSearch)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _searchBox.Focus();
                    _searchBox.SelectAll();
                }), DispatcherPriority.Input);
            }
        }

        public void CreateAndEdit()
        {
            ShowAndFocus(false);
            _viewModel.CreateEntry();
            RefreshAll();
            Dispatcher.BeginInvoke(new Action(delegate
            {
                _titleBox.Focus();
                _titleBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        public void ReportStatus(string message, bool isError)
        {
            _viewModel.ReportStatus(message, isError);
        }

        public void SaveCurrentBounds()
        {
            if (WindowState == WindowState.Normal)
            {
                _viewModel.UpdateWindowBounds(Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _resultsRefreshTimer.Stop();
            _resultsRefreshTimer.Tick -= ResultsRefreshTimerOnTick;
            _viewModel.ContentChanged -= ViewModelOnContentChanged;
            _viewModel.SelectedEntryChanged -= ViewModelOnSelectedEntryChanged;
            _viewModel.StatusChanged -= ViewModelOnStatusChanged;
            PreviewKeyDown -= MainWindowOnPreviewKeyDown;
            LocationChanged -= WindowBoundsOnChanged;
            SizeChanged -= WindowBoundsOnChanged;
            Loaded -= MainWindowOnLoaded;
            if (_mainGrid != null)
            {
                _mainGrid.SizeChanged -= MainGridOnSizeChanged;
            }
            _viewModel.Dispose();
            _stickyWindows.Dispose();
            ThemeChanged = null;
        }

        private UIElement BuildWindowContent()
        {
            var root = new Grid { Background = Brush(ThemeResources.CanvasBrushKey) };
            root.SetValue(TextElement.ForegroundProperty, Brush(ThemeResources.InkBrushKey));
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

            _mainGrid = new Grid();
            AutomationProperties.SetName(_mainGrid, "主响应式布局");
            _sidebarColumn = new ColumnDefinition();
            _listColumn = new ColumnDefinition();
            _editorColumn = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
            _mainGrid.ColumnDefinitions.Add(_sidebarColumn);
            _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            _mainGrid.ColumnDefinitions.Add(_listColumn);
            _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            _mainGrid.ColumnDefinitions.Add(_editorColumn);
            _mainGrid.Children.Add(BuildSidebar());
            _mainGrid.Children.Add(SeparatorAtColumn(1));
            UIElement listPane = BuildListPane();
            Grid.SetColumn(listPane, 2);
            _mainGrid.Children.Add(listPane);
            _mainGrid.Children.Add(SeparatorAtColumn(3));
            UIElement editor = BuildEditorPane();
            Grid.SetColumn(editor, 4);
            _mainGrid.Children.Add(editor);
            ApplyResponsiveLayout(Width);
            _mainGrid.SizeChanged += MainGridOnSizeChanged;
            root.Children.Add(_mainGrid);

            UIElement status = BuildStatusBar();
            Grid.SetRow(status, 1);
            root.Children.Add(status);
            return root;
        }

        private UIElement BuildSidebar()
        {
            var panel = new DockPanel
            {
                Background = Brush(ThemeResources.SurfaceBrushKey),
                LastChildFill = true
            };

            var footer = new StackPanel();
            var settings = QuietButton("设置", "设置关闭按钮的行为");
            settings.Style = (Style)FindResource("Seer.QuietButton");
            settings.HorizontalContentAlignment = HorizontalAlignment.Left;
            settings.MinHeight = 32;
            settings.Padding = new Thickness(10, 4, 10, 4);
            settings.Margin = new Thickness(0, 0, 0, 2);
            settings.Click += SettingsOnClick;
            var export = QuietButton("导出完整备份", "导出全部条目和设置为 JSON 文件");
            export.Style = (Style)FindResource("Seer.QuietButton");
            export.HorizontalContentAlignment = HorizontalAlignment.Left;
            export.MinHeight = 32;
            export.Padding = new Thickness(10, 4, 10, 4);
            export.Margin = new Thickness(0, 0, 0, 2);
            export.Click += ExportOnClick;
            var exit = QuietButton("退出 SeerNote", "保存并完全退出 SeerNote");
            exit.Style = (Style)FindResource("Seer.QuietButton");
            exit.HorizontalContentAlignment = HorizontalAlignment.Left;
            exit.MinHeight = 32;
            exit.Padding = new Thickness(10, 4, 10, 4);
            exit.Click += delegate { RaiseExitRequested(); };
            footer.Children.Add(settings);
            footer.Children.Add(export);
            footer.Children.Add(exit);
            var footerSurface = new Border
            {
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 8, 12, 8),
                Child = footer
            };
            AutomationProperties.SetName(footerSurface, "本地工作区操作");
            DockPanel.SetDock(footerSurface, Dock.Bottom);
            panel.Children.Add(footerSurface);

            var content = new StackPanel { Margin = new Thickness(14, 18, 14, 10) };
            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 18) };
            var mark = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(10),
                Background = Brush(ThemeResources.SurfaceRaisedBrushKey),
                BorderBrush = Brush(ThemeResources.GoldBrushKey),
                BorderThickness = new Thickness(1),
                Child = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(8),
                    Background = Brush(ThemeResources.AccentBrushKey),
                    BorderBrush = Brush(ThemeResources.FocusBrushKey),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            string displayVersion = GetDisplayVersion();
            var version = new TextBlock
            {
                Text = displayVersion,
                FontSize = 9,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            AutomationProperties.SetName(version, "SeerNote 版本 " + displayVersion.TrimStart('v'));
            var markColumn = new StackPanel { Width = 46 };
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            markColumn.Children.Add(mark);
            markColumn.Children.Add(version);
            var name = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = "SeerNote", FontSize = 18, FontWeight = FontWeights.SemiBold });
            name.Children.Add(new TextBlock { Text = "LOCAL NOTE", FontSize = 9.5, Foreground = Brush(ThemeResources.MutedBrushKey), Margin = new Thickness(0, 2, 0, 0) });
            brand.Children.Add(markColumn);
            brand.Children.Add(name);
            content.Children.Add(brand);

            content.Children.Add(SectionLabel("快速访问"));
            AddViewButton(content, SmartView.Favorite, "收藏置顶", "只显示收藏置顶的条目");
            AddViewButton(content, SmartView.All, "所有条目", "显示所有未删除条目");

            _categorySidebar = new CategorySidebar();
            _categorySidebar.CreateRequested += CategoryCreateOnRequested;
            _categorySidebar.CategorySelected += CategorySidebarOnCategorySelected;
            _categorySidebar.RenameRequested += CategoryRenameOnRequested;
            _categorySidebar.DeleteRequested += CategoryDeleteOnRequested;
            _categorySidebar.ReorderRequested += CategoryReorderOnRequested;
            _categorySidebar.EntryMoveRequested += EntryCategoryMoveOnRequested;
            content.Children.Add(_categorySidebar);

            AddViewButton(content, SmartView.Trash, "回收站", "显示已删除条目");
            var scroll = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            panel.Children.Add(scroll);
            return panel;
        }

        private UIElement BuildListPane()
        {
            var root = new Grid
            {
                Background = Brush(ThemeResources.CanvasBrushKey),
                Margin = new Thickness(16, 15, 16, 12)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var libraryHeader = new Grid { Margin = new Thickness(1, 0, 0, 11) };
            libraryHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            libraryHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            heading.Children.Add(new TextBlock
            {
                Text = "NOTE LIBRARY",
                FontSize = 9.5,
                Foreground = Brush(ThemeResources.MutedBrushKey)
            });
            heading.Children.Add(new TextBlock
            {
                Text = "笔记库",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 1, 0, 0)
            });
            libraryHeader.Children.Add(heading);
            var createButton = new Button
            {
                Content = "＋ 新建",
                Style = (Style)FindResource("Seer.ToolbarButton"),
                MinWidth = 82,
                Height = 36,
                Margin = new Thickness(10, 0, 0, 0)
            };
            AutomationProperties.SetName(createButton, "新建条目");
            AutomationProperties.SetHelpText(createButton, "新建一条本地 Note。快捷键 Ctrl+N。");
            createButton.Click += delegate { CreateAndEdit(); };
            Grid.SetColumn(createButton, 1);
            libraryHeader.Children.Add(createButton);
            root.Children.Add(libraryHeader);

            var searchHost = new Grid { Height = 42, Margin = new Thickness(0, 0, 0, 12) };
            _searchBox = new TextBox
            {
                ToolTip = "搜索标题、正文和分类（Ctrl+F）",
                Height = 42,
                Padding = new Thickness(34, 8, 62, 8),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 14.0
            };
            AutomationProperties.SetName(_searchBox, "搜索条目");
            AutomationProperties.SetHelpText(_searchBox, "输入中文或英文，结果会即时更新。可用清空按钮或 Esc 返回全部条目。快捷键 Ctrl+F。");
            _searchBox.TextChanged += SearchBoxOnTextChanged;
            _searchBox.PreviewKeyDown += SearchBoxOnPreviewKeyDown;
            searchHost.Children.Add(_searchBox);
            var searchGlyph = new Grid
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(11, 0, 0, 0),
                IsHitTestVisible = false
            };
            searchGlyph.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                BorderBrush = Brush(ThemeResources.MutedBrushKey),
                BorderThickness = new Thickness(1.4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(1, 1, 0, 0)
            });
            searchGlyph.Children.Add(new Border
            {
                Width = 7,
                Height = 1.4,
                Background = Brush(ThemeResources.MutedBrushKey),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 1, 3),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(45)
            });
            searchHost.Children.Add(searchGlyph);
            _searchPlaceholder = new TextBlock
            {
                Text = "搜索 Note…",
                Foreground = Brush(ThemeResources.MutedBrushKey),
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(35, 0, 62, 0),
                IsHitTestVisible = false
            };
            searchHost.Children.Add(_searchPlaceholder);
            _searchShortcut = new Border
            {
                Background = Brush(ThemeResources.SurfaceBrushKey),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = "Ctrl F",
                    FontSize = 9.5,
                    Foreground = Brush(ThemeResources.MutedBrushKey)
                }
            };
            AutomationProperties.SetName(_searchShortcut, "搜索快捷键提示");
            searchHost.Children.Add(_searchShortcut);
            _clearSearchButton = new Button
            {
                Content = "×",
                Style = (Style)FindResource("Seer.QuietButton"),
                ToolTip = "清空搜索（Esc）",
                Width = 34,
                Height = 34,
                Padding = new Thickness(0),
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Collapsed
            };
            AutomationProperties.SetName(_clearSearchButton, "清空搜索");
            AutomationProperties.SetHelpText(_clearSearchButton, "清空当前搜索并将焦点返回搜索框。快捷键 Esc。");
            _clearSearchButton.Click += delegate { ClearSearchAndFocus(); };
            searchHost.Children.Add(_clearSearchButton);
            Grid.SetRow(searchHost, 1);
            root.Children.Add(searchHost);

            var resultsHeader = new DockPanel { Margin = new Thickness(1, 0, 1, 8) };
            _emptyTrashButton = new Button
            {
                Content = "清空回收站",
                Style = (Style)FindResource("Seer.DangerButton"),
                MinHeight = 26,
                Padding = new Thickness(8, 2, 8, 2),
                Visibility = Visibility.Collapsed
            };
            AutomationProperties.SetName(_emptyTrashButton, "清空回收站");
            AutomationProperties.SetHelpText(_emptyTrashButton, "永久删除回收站中的全部内容");
            _emptyTrashButton.Click += ClearTrashOnClick;
            DockPanel.SetDock(_emptyTrashButton, Dock.Right);
            resultsHeader.Children.Add(_emptyTrashButton);

            _resultCount = new TextBlock
            {
                Foreground = Brush(ThemeResources.MutedBrushKey),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            resultsHeader.Children.Add(_resultCount);
            Grid.SetRow(resultsHeader, 2);
            root.Children.Add(resultsHeader);

            var resultsHost = new Grid();
            _entryList = new EntryListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0, 0, 2, 0),
                ContextMenuFactory = CreateEntryContextMenu
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_entryList, ScrollBarVisibility.Disabled);
            AutomationProperties.SetName(_entryList, "条目结果");
            AutomationProperties.SetHelpText(_entryList, "使用上下方向键选择，按 Enter 进入正文；搜索生效时按 Esc 清空。可拖到左侧分类中移动。");
            _entryList.SelectionChanged += EntryListOnSelectionChanged;
            _entryList.PreviewKeyDown += EntryListOnPreviewKeyDown;
            _entryList.PreviewMouseRightButtonDown += EntryListOnPreviewMouseRightButtonDown;
            _entryList.PreviewMouseLeftButtonDown += EntryListOnPreviewMouseLeftButtonDown;
            _entryList.PreviewMouseMove += EntryListOnPreviewMouseMove;
            resultsHost.Children.Add(_entryList);

            _emptyResults = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 260,
                Margin = new Thickness(10)
            };
            _emptyResultsText = new TextBlock
            {
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                Margin = new Thickness(0, 0, 0, 12)
            };
            _emptyCreateButton = new Button
            {
                Content = "写下第一条",
                Style = (Style)FindResource("Seer.PrimaryButton")
            };
            AutomationProperties.SetName(_emptyCreateButton, "从空状态新建条目");
            _emptyCreateButton.Click += delegate { CreateAndEdit(); };
            _emptyResults.Children.Add(_emptyResultsText);
            _emptyResults.Children.Add(_emptyCreateButton);
            resultsHost.Children.Add(_emptyResults);
            Grid.SetRow(resultsHost, 3);
            root.Children.Add(resultsHost);
            return root;
        }

        private UIElement BuildEditorPane()
        {
            var host = new Grid
            {
                Background = Brush(ThemeResources.SurfaceBrushKey),
                Margin = new Thickness(0)
            };
            _editorEmptyTitle = new TextBlock
            {
                Text = "选择一条内容开始编辑",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };
            _editorEmptyHint = new TextBlock
            {
                Text = "或按 Ctrl+N 新建条目",
                Margin = new Thickness(0, 7, 0, 0),
                Foreground = Brush(ThemeResources.MutedBrushKey),
                TextAlignment = TextAlignment.Center
            };
            _editorEmpty = new Border
            {
                Margin = new Thickness(24),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        _editorEmptyTitle,
                        _editorEmptyHint
                    }
                }
            };
            host.Children.Add(_editorEmpty);

            _editorContent = new Grid();
            _editorContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _editorContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _editorContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _editorContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var documentStrip = new Border
            {
                Background = Brush(ThemeResources.CanvasBrushKey),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 10, 18, 0)
            };
            AutomationProperties.SetName(documentStrip, "当前 Note 标签栏");
            var documentTab = new Border
            {
                MinWidth = 240,
                MaxWidth = 520,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Brush(ThemeResources.SurfaceBrushKey),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(1, 1, 1, 0),
                CornerRadius = new CornerRadius(7, 7, 0, 0),
                Padding = new Thickness(4, 2, 4, 2)
            };
            AutomationProperties.SetName(documentTab, "当前 Note 标签");
            _titleBox = new TextBox
            {
                FontSize = 16.5,
                FontWeight = FontWeights.SemiBold,
                MinHeight = 40,
                Padding = new Thickness(10, 7, 10, 7),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(_titleBox, "条目标题");
            AutomationProperties.SetHelpText(_titleBox, "当前 Note 的标题；停顿后自动保存。");
            _titleBox.TextChanged += TitleBoxOnTextChanged;
            documentTab.Child = _titleBox;
            var documentStripGrid = new Grid();
            documentStripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            documentStripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            documentStripGrid.Children.Add(documentTab);
            _documentStateText = new TextBlock
            {
                Text = "已保存到本地",
                FontSize = 10.5,
                Foreground = Brush(ThemeResources.SuccessBrushKey),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 2, 8)
            };
            AutomationProperties.SetName(_documentStateText, "当前 Note 保存状态");
            Grid.SetColumn(_documentStateText, 1);
            documentStripGrid.Children.Add(_documentStateText);
            documentStrip.Child = documentStripGrid;
            _editorContent.Children.Add(documentStrip);

            var commandBar = new Border
            {
                Background = Brush(ThemeResources.SurfaceRaisedBrushKey),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 6, 18, 6)
            };
            AutomationProperties.SetName(commandBar, "Note 命令栏");
            var commands = new WrapPanel { ItemHeight = 34 };
            commands.Children.Add(new TextBlock
            {
                Text = "分类",
                FontSize = 12,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0)
            });
            _categoryBox = new ComboBox
            {
                Width = 180,
                Height = 34,
                Padding = new Thickness(9, 5, 9, 5),
                Background = Brush(ThemeResources.SurfaceRaisedBrushKey),
                Foreground = Brush(ThemeResources.InkBrushKey),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            AutomationProperties.SetName(_categoryBox, "条目分类");
            AutomationProperties.SetHelpText(_categoryBox, "选择一个自定义分类；分类可在左侧创建和排序。");
            _categoryBox.SelectionChanged += CategoryBoxOnSelectionChanged;
            commands.Children.Add(_categoryBox);
            _favoriteButton = new Button
            {
                MinWidth = 86,
                Height = 34,
                Content = "☆ 收藏",
                ToolTip = "在列表中优先显示（Ctrl+Shift+P）",
                Style = (Style)FindResource("Seer.ToolbarButton"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            AutomationProperties.SetName(_favoriteButton, "切换收藏置顶");
            _favoriteButton.Click += delegate { _viewModel.ToggleFavorite(); };
            commands.Children.Add(_favoriteButton);
            _stickyButton = new Button
            {
                Content = "置顶小窗",
                Height = 34,
                Style = (Style)FindResource("Seer.ToolbarButton")
            };
            AutomationProperties.SetName(_stickyButton, "打开置顶小窗");
            _stickyButton.Click += delegate { OpenSelectedSticky(); };
            commands.Children.Add(_stickyButton);
            commandBar.Child = commands;
            Grid.SetRow(commandBar, 1);
            _editorContent.Children.Add(commandBar);

            var bodySurface = new Border
            {
                Background = Brush(ThemeResources.SurfaceBrushKey),
                Padding = new Thickness(8, 0, 8, 0)
            };
            AutomationProperties.SetName(bodySurface, "纯文本编辑画布");
            _bodyBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FontSize = 16,
                Padding = new Thickness(28, 24, 28, 24),
                Background = Brush(ThemeResources.SurfaceBrushKey),
                BorderThickness = new Thickness(0),
                FontFamily = (FontFamily)FindResource(ThemeResources.EditorFontFamilyKey),
                MaxWidth = 1040,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SpellCheck.SetIsEnabled(_bodyBox, false);
            AutomationProperties.SetName(_bodyBox, "条目正文");
            AutomationProperties.SetHelpText(_bodyBox, "纯文本编辑区；停顿后自动保存。需要复用时可使用双花括号变量。");
            _bodyBox.TextChanged += BodyBoxOnTextChanged;
            bodySurface.Child = _bodyBox;
            Grid.SetRow(bodySurface, 2);
            _editorContent.Children.Add(bodySurface);

            var actionBar = new Border
            {
                Background = Brush(ThemeResources.SurfaceRaisedBrushKey),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 7, 18, 7)
            };
            AutomationProperties.SetName(actionBar, "Note 底部操作区");
            var actionGrid = new Grid();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var agentHandoff = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 12, 0)
            };
            AutomationProperties.SetName(agentHandoff, "智能体交接操作区");
            agentHandoff.Children.Add(new TextBlock
            {
                Text = "智能体交接",
                FontSize = 10.5,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                Margin = new Thickness(3, 0, 0, 3)
            });
            var agentActions = new WrapPanel { ItemHeight = 34 };
            _copyButton = new Button
            {
                Content = "复制正文",
                Style = (Style)FindResource("Seer.PrimaryButton"),
                Padding = new Thickness(12, 6, 12, 6),
                ToolTip = "复制正文（Ctrl+Enter）",
                Margin = new Thickness(0, 0, 6, 0)
            };
            AutomationProperties.SetName(_copyButton, "复制正文");
            AutomationProperties.SetHelpText(_copyButton, "复制当前 Note 正文；包含双花括号变量时先填写变量。快捷键 Ctrl+Enter。");
            _copyButton.Click += delegate { CopySelected(); };
            var copyIdButton = new Button
            {
                Content = "复制 ID",
                Style = (Style)FindResource("Seer.ToolbarButton"),
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = "复制稳定的 Note UUID"
            };
            AutomationProperties.SetName(copyIdButton, "复制 Note ID");
            AutomationProperties.SetHelpText(copyIdButton, "复制当前 Note 的稳定 UUID，便于命令行或智能体继续操作。");
            copyIdButton.Click += delegate { CopySelectedId(); };
            var copyJsonButton = new Button
            {
                Content = "复制为 JSON",
                Style = (Style)FindResource("Seer.ToolbarButton"),
                Padding = new Thickness(9, 5, 9, 5),
                ToolTip = "复制 seernote.note.v1 结构化数据"
            };
            AutomationProperties.SetName(copyJsonButton, "复制为 JSON");
            AutomationProperties.SetHelpText(copyJsonButton, "复制当前 Note 的结构化 JSON，包括 ID、正文、分类和时间戳。");
            copyJsonButton.Click += delegate { CopySelectedJson(); };
            agentActions.Children.Add(_copyButton);
            agentActions.Children.Add(copyIdButton);
            agentActions.Children.Add(copyJsonButton);
            agentHandoff.Children.Add(agentActions);
            actionGrid.Children.Add(agentHandoff);

            var destructiveActions = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                ItemHeight = 34
            };
            AutomationProperties.SetName(destructiveActions, "Note 删除操作区");
            _deleteButton = new Button
            {
                Content = "移到回收站",
                Style = (Style)FindResource("Seer.DangerButton")
            };
            AutomationProperties.SetName(_deleteButton, "移到回收站");
            _deleteButton.Click += delegate { SoftDeleteSelected(); };
            _restoreButton = new Button
            {
                Content = "还原 Note",
                Style = (Style)FindResource("Seer.PrimaryButton"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            AutomationProperties.SetName(_restoreButton, "还原条目");
            _restoreButton.Click += delegate { _viewModel.RestoreSelected(); };
            _permanentDeleteButton = new Button
            {
                Content = "永久删除",
                Style = (Style)FindResource("Seer.DangerButton")
            };
            AutomationProperties.SetName(_permanentDeleteButton, "永久删除条目");
            _permanentDeleteButton.Click += delegate { PermanentlyDeleteSelected(); };
            destructiveActions.Children.Add(_deleteButton);
            destructiveActions.Children.Add(_restoreButton);
            destructiveActions.Children.Add(_permanentDeleteButton);
            Grid.SetColumn(destructiveActions, 1);
            actionGrid.Children.Add(destructiveActions);
            actionBar.Child = actionGrid;
            Grid.SetRow(actionBar, 3);
            _editorContent.Children.Add(actionBar);
            host.Children.Add(_editorContent);
            return host;
        }

        private UIElement BuildStatusBar()
        {
            var border = new Border
            {
                Background = Brush(ThemeResources.SurfaceRaisedBrushKey),
                BorderBrush = Brush(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 0, 12, 0)
            };
            var dock = new DockPanel { LastChildFill = true };
            var shortcut = new TextBlock
            {
                Text = "Ctrl+F 搜索  ·  Ctrl+N 新建  ·  Ctrl+S 保存",
                FontSize = 11,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(shortcut, Dock.Right);
            dock.Children.Add(shortcut);
            _retrySaveButton = new Button
            {
                Content = "重试保存",
                Padding = new Thickness(7, 1, 7, 1),
                MinHeight = 24,
                Margin = new Thickness(8, 3, 0, 3),
                Visibility = Visibility.Collapsed
            };
            _retrySaveButton.Click += delegate { _viewModel.RequestImmediateSave(); };
            DockPanel.SetDock(_retrySaveButton, Dock.Right);
            dock.Children.Add(_retrySaveButton);
            _statusText = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(_statusText, "应用状态：就绪");
            AutomationProperties.SetLiveSetting(_statusText, AutomationLiveSetting.Polite);
            dock.Children.Add(_statusText);
            border.Child = dock;
            return border;
        }

        private void RefreshAll()
        {
            if (_disposed)
            {
                return;
            }
            _refreshing = true;
            try
            {
                if (!String.Equals(_searchBox.Text, _viewModel.SearchText, StringComparison.Ordinal))
                {
                    _searchBox.Text = _viewModel.SearchText;
                }
                RefreshNavigation();
                RefreshResults();
                RefreshEditor();
                RefreshStatus();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void RefreshNavigation()
        {
            NavigationSnapshot snapshot = _viewModel.GetNavigationSnapshot();
            bool selectionChanged = _lastNavigationSnapshot == null || _lastNavigationView != _viewModel.SelectedView || !String.Equals(_lastNavigationCategory, _viewModel.SelectedCategory, StringComparison.InvariantCultureIgnoreCase);
            if (!selectionChanged && (ReferenceEquals(snapshot, _lastNavigationSnapshot) || snapshot.HasSameContent(_lastNavigationSnapshot)))
            {
                _lastNavigationSnapshot = snapshot;
                return;
            }
            foreach (KeyValuePair<SmartView, Button> pair in _viewButtons)
            {
                bool selected = pair.Key == _viewModel.SelectedView && _viewModel.SelectedCategory == null;
                pair.Value.Background = selected ? Brush(ThemeResources.SurfaceRaisedBrushKey) : Brushes.Transparent;
                pair.Value.BorderBrush = selected ? Brush(ThemeResources.AccentBrushKey) : Brushes.Transparent;
                pair.Value.Foreground = selected ? Brush(ThemeResources.InkBrushKey) : Brush(ThemeResources.InkBrushKey);
                TextBlock countText;
                if (_viewCounts.TryGetValue(pair.Key, out countText))
                {
                    int count = pair.Key == SmartView.Favorite ? snapshot.FavoriteCount : pair.Key == SmartView.Trash ? snapshot.TrashCount : snapshot.AllCount;
                    countText.Text = count.ToString();
                    countText.Foreground = selected ? Brush(ThemeResources.AccentBrushKey) : Brush(ThemeResources.MutedBrushKey);
                }
            }
            _categorySidebar.Refresh(snapshot.Categories, _viewModel.SelectedCategory, snapshot.CategoryCounts);
            _lastNavigationSnapshot = snapshot;
            _lastNavigationView = _viewModel.SelectedView;
            _lastNavigationCategory = _viewModel.SelectedCategory;
        }

        private void RefreshResults()
        {
            Guid selectedId = _viewModel.SelectedEntry == null ? Guid.Empty : _viewModel.SelectedEntry.Id;
            IList<Entry> entries = _viewModel.GetFilteredEntries();
            Entry selectedEntry = entries.FirstOrDefault(entry => entry.Id == selectedId);
            _entryList.ItemsSource = entries;
            _entryList.SelectedItem = selectedEntry;
            if (selectedEntry != null)
            {
                _entryList.ScrollIntoView(selectedEntry);
            }
            _resultCount.Text = CurrentScopeLabel() + "  ·  " + entries.Count + " 条";
            bool isTrashView = _viewModel.SelectedView == SmartView.Trash && _viewModel.SelectedCategory == null;
            int trashCount = _viewModel.TrashCount;
            _emptyTrashButton.Visibility = isTrashView && trashCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            _emptyTrashButton.IsEnabled = true;
            AutomationProperties.SetHelpText(_emptyTrashButton, trashCount == 0
                ? "回收站已经是空的"
                : "永久删除回收站中的全部 " + trashCount + " 条内容");
            bool empty = entries.Count == 0;
            _entryList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            _emptyResults.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            if (empty)
            {
                string query = (_viewModel.SearchText ?? String.Empty).Trim();
                if (isTrashView)
                {
                    _emptyResultsText.Text = query.Length == 0 ? "回收站为空。" : "回收站中没有匹配内容。";
                    _emptyCreateButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _emptyResultsText.Text = query.Length == 0
                        ? "还没有条目。按 Ctrl+N 写下第一条。"
                        : "没有匹配内容。可用当前文字新建条目。";
                    _emptyCreateButton.Content = query.Length == 0
                        ? "写下第一条"
                        : "以“" + Truncate(query, 16) + "”新建";
                    _emptyCreateButton.Visibility = Visibility.Visible;
                }
            }
        }

        private void RefreshEditor()
        {
            Entry entry = _viewModel.SelectedEntry;
            bool hasEntry = entry != null;
            _editorContent.Visibility = hasEntry ? Visibility.Visible : Visibility.Collapsed;
            _editorEmpty.Visibility = hasEntry ? Visibility.Collapsed : Visibility.Visible;
            _editorEmptyTitle.Text = "选择一条 Note 开始编辑";
            _editorEmptyHint.Text = "或按 Ctrl+N 新建条目";
            if (!hasEntry)
            {
                return;
            }

            SetTextPreservingCaret(_titleBox, entry.Title ?? String.Empty);
            SetTextPreservingCaret(_bodyBox, entry.Body ?? String.Empty);
            RefreshCategoryPicker(entry.Category);
            _favoriteButton.Content = entry.IsFavorite ? "★ 已收藏" : "☆ 收藏";
            _favoriteButton.Foreground = entry.IsFavorite ? Brush(ThemeResources.GoldBrushKey) : Brush(ThemeResources.InkBrushKey);
            _favoriteButton.Background = Brush(ThemeResources.SurfaceBrushKey);
            _favoriteButton.BorderBrush = entry.IsFavorite ? Brush(ThemeResources.GoldBrushKey) : Brush(ThemeResources.BorderBrushKey);

            bool editable = !entry.IsDeleted;
            _titleBox.IsReadOnly = !editable;
            _bodyBox.IsReadOnly = !editable;
            _categoryBox.IsEnabled = editable;
            _favoriteButton.IsEnabled = editable;
            _copyButton.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
            _stickyButton.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
            _deleteButton.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
            _restoreButton.Visibility = editable ? Visibility.Collapsed : Visibility.Visible;
            _permanentDeleteButton.Visibility = editable ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RefreshStatus()
        {
            _statusText.Text = _viewModel.StatusText;
            _statusText.Foreground = Brush(_viewModel.StatusIsError ? ThemeResources.DangerBrushKey : ThemeResources.MutedBrushKey);
            AutomationProperties.SetName(_statusText, "应用状态：" + _viewModel.StatusText);
            AutomationProperties.SetLiveSetting(_statusText, _viewModel.StatusIsError ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
            _retrySaveButton.Visibility = _viewModel.StatusIsError && _viewModel.HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
            if (_documentStateText != null)
            {
                Entry selectedEntry = _viewModel.SelectedEntry;
                if (selectedEntry != null && selectedEntry.IsDeleted)
                {
                    _documentStateText.Text = "只读 · 回收站";
                    _documentStateText.Foreground = Brush(ThemeResources.WarningBrushKey);
                }
                else if (_viewModel.StatusIsError && _viewModel.HasUnsavedChanges)
                {
                    _documentStateText.Text = "保存需要处理";
                    _documentStateText.Foreground = Brush(ThemeResources.DangerBrushKey);
                }
                else if (_viewModel.HasUnsavedChanges)
                {
                    _documentStateText.Text = "正在保存到本地…";
                    _documentStateText.Foreground = Brush(ThemeResources.WarningBrushKey);
                }
                else
                {
                    _documentStateText.Text = "已保存到本地";
                    _documentStateText.Foreground = Brush(ThemeResources.SuccessBrushKey);
                }
                AutomationProperties.SetHelpText(_documentStateText, _viewModel.StatusText);
            }
            AnnounceStatusIfNeeded();
        }

        private void AnnounceStatusIfNeeded()
        {
            if (!IsLoaded || !_viewModel.StatusShouldAnnounce || _lastAnnouncedStatusRevision == _viewModel.StatusRevision)
            {
                return;
            }
            _lastAnnouncedStatusRevision = _viewModel.StatusRevision;
            AutomationPeer peer = FrameworkElementAutomationPeer.FromElement(_statusText);
            if (peer != null)
            {
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        private void RefreshCategoryPicker(string selectedCategory)
        {
            NavigationSnapshot snapshot = _viewModel.GetNavigationSnapshot();
            IList<string> categories = snapshot.Categories;
            if (_lastCategoryPickerSnapshot == null || !snapshot.HasSameCategoryOrder(_lastCategoryPickerSnapshot))
            {
                _categoryBox.Items.Clear();
                _categoryBox.Items.Add("未分类");
                for (int index = 0; index < categories.Count; index++)
                {
                    _categoryBox.Items.Add(categories[index]);
                }
            }

            int selectedIndex = 0;
            for (int index = 0; index < categories.Count; index++)
            {
                string category = categories[index];
                if (String.Equals(category, selectedCategory, StringComparison.InvariantCultureIgnoreCase))
                {
                    selectedIndex = index + 1;
                    break;
                }
            }
            if (_categoryBox.SelectedIndex != selectedIndex)
            {
                _categoryBox.SelectedIndex = selectedIndex;
            }
            _lastCategoryPickerSnapshot = snapshot;
        }

        private ContextMenu CreateEntryContextMenu(Entry entry)
        {
            if (_entryList != null && _entryList.Items.Count <= 6)
            {
                return CreateDirectEntryContextMenu(entry);
            }
            var menu = new EntryContextMenu(
                entry.IsDeleted,
                GetEntryMenuCategories,
                CopyEntryFromMenu,
                CopyEntryIdFromMenu,
                CopyEntryJsonFromMenu,
                ToggleFavoriteFromMenu,
                OpenStickyFromMenu,
                SoftDeleteFromMenu,
                RestoreFromMenu,
                PermanentlyDeleteFromMenu,
                MoveEntryFromMenu);
            return menu;
        }

        private ContextMenu CreateDirectEntryContextMenu(Entry entry)
        {
            var menu = new ContextMenu();
            if (entry.IsDeleted)
            {
                AddAgentCopyItems(menu);
                menu.Items.Add(new Separator());
                var restore = new MenuItem { Header = "还原 Note" };
                restore.Click += DirectRestoreOnClick;
                var permanentDelete = new MenuItem { Header = "永久删除" };
                permanentDelete.Click += DirectPermanentDeleteOnClick;
                menu.Items.Add(restore);
                menu.Items.Add(new Separator());
                menu.Items.Add(permanentDelete);
                return menu;
            }

            var copy = new MenuItem { Header = "复制正文" };
            copy.Click += DirectCopyBodyOnClick;
            var favorite = new MenuItem { Header = entry.IsFavorite ? "取消收藏置顶" : "收藏置顶" };
            favorite.Click += DirectToggleFavoriteOnClick;
            var sticky = new MenuItem { Header = "打开置顶小窗" };
            sticky.Click += DirectOpenStickyOnClick;
            var move = new MenuItem { Header = "移动到分类" };
            var uncategorized = new MenuItem
            {
                Header = "未分类",
                IsCheckable = true,
                IsChecked = String.IsNullOrWhiteSpace(entry.Category)
            };
            uncategorized.Click += delegate { MoveEntryFromMenu(entry, null); };
            move.Items.Add(uncategorized);
            foreach (string category in _viewModel.GetCategories())
            {
                string destination = category;
                var categoryItem = new MenuItem
                {
                    Header = destination,
                    IsCheckable = true,
                    IsChecked = String.Equals(entry.Category, destination, StringComparison.InvariantCultureIgnoreCase)
                };
                categoryItem.Click += delegate { MoveEntryFromMenu(entry, destination); };
                move.Items.Add(categoryItem);
            }
            var delete = new MenuItem { Header = "移到回收站" };
            delete.Click += DirectSoftDeleteOnClick;

            menu.Items.Add(copy);
            AddAgentCopyItems(menu);
            menu.Items.Add(new Separator());
            menu.Items.Add(favorite);
            menu.Items.Add(sticky);
            menu.Items.Add(move);
            menu.Items.Add(new Separator());
            menu.Items.Add(delete);
            return menu;
        }

        private void AddAgentCopyItems(ContextMenu menu)
        {
            var copyId = new MenuItem { Header = "复制 Note ID" };
            copyId.Click += DirectCopyIdOnClick;
            var copyJson = new MenuItem { Header = "复制为 JSON" };
            copyJson.Click += DirectCopyJsonOnClick;
            menu.Items.Add(copyId);
            menu.Items.Add(copyJson);
        }

        private Entry TakeDirectEntryMenuTarget(object sender)
        {
            var item = sender as MenuItem;
            var menu = item == null ? null : ItemsControl.ItemsControlFromItemContainer(item) as ContextMenu;
            Entry entry = menu == null ? null : menu.Tag as Entry;
            if (menu != null)
            {
                menu.Tag = null;
            }
            return entry;
        }

        private void DirectCopyBodyOnClick(object sender, RoutedEventArgs eventArgs)
        {
            CopyEntryFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private void DirectCopyIdOnClick(object sender, RoutedEventArgs eventArgs)
        {
            CopyEntryIdFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private void DirectCopyJsonOnClick(object sender, RoutedEventArgs eventArgs)
        {
            CopyEntryJsonFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private void DirectToggleFavoriteOnClick(object sender, RoutedEventArgs eventArgs)
        {
            ToggleFavoriteFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private void DirectOpenStickyOnClick(object sender, RoutedEventArgs eventArgs)
        {
            OpenStickyFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private void DirectSoftDeleteOnClick(object sender, RoutedEventArgs eventArgs)
        {
            SoftDeleteFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private void DirectRestoreOnClick(object sender, RoutedEventArgs eventArgs)
        {
            RestoreFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private void DirectPermanentDeleteOnClick(object sender, RoutedEventArgs eventArgs)
        {
            PermanentlyDeleteFromMenu(TakeDirectEntryMenuTarget(sender));
        }

        private IList<string> GetEntryMenuCategories()
        {
            return _viewModel.GetNavigationSnapshot().Categories;
        }

        private void MoveEntryFromMenu(Guid entryId, string category)
        {
            if (_viewModel.MoveEntryToCategory(entryId, category))
            {
                _viewModel.ReportStatus("已移动到“" + (String.IsNullOrWhiteSpace(category) ? "未分类" : category) + "”。", false);
            }
        }

        private void MoveEntryFromMenu(Entry entry, string category)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                MoveEntryFromMenu(entry.Id, category);
            }
        }

        private bool SelectEntryForMenuCommand(Entry entry)
        {
            if (entry == null || !_viewModel.State.Entries.Contains(entry))
            {
                return false;
            }
            _viewModel.SelectEntry(entry);
            return true;
        }

        private void CopyEntryFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                CopySelected();
            }
        }

        private void CopyEntryIdFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                CopySelectedId();
            }
        }

        private void CopyEntryJsonFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                CopySelectedJson();
            }
        }

        private void ToggleFavoriteFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                _viewModel.ToggleFavorite();
            }
        }

        private void OpenStickyFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                OpenSelectedSticky();
            }
        }

        private void SoftDeleteFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                SoftDeleteSelected();
            }
        }

        private void RestoreFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                _viewModel.RestoreSelected();
            }
        }

        private void PermanentlyDeleteFromMenu(Entry entry)
        {
            if (SelectEntryForMenuCommand(entry))
            {
                PermanentlyDeleteSelected();
            }
        }

        private void CopySelected()
        {
            Entry entry = _viewModel.SelectedEntry;
            if (entry == null || entry.IsDeleted)
            {
                return;
            }
            string source = entry.Body ?? String.Empty;
            if (source.Length == 0)
            {
                _viewModel.ReportStatus("正文为空，没有可复制的内容。", true);
                return;
            }

            string text = source;
            IList<string> variables = PromptTemplate.Parse(source);
            if (variables.Count > 0)
            {
                IReadOnlyDictionary<string, string> collected;
                if (!VariableDialog.TryCollect(this, variables, out collected))
                {
                    _viewModel.ReportStatus("已取消复制，变量值未保存。", false);
                    return;
                }
                var values = collected.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                string error;
                if (!PromptTemplate.TryRender(source, values, out text, out error))
                {
                    _viewModel.ReportStatus("变量处理失败：" + error, true);
                    return;
                }
            }
            _viewModel.CopyText(text);
        }

        private void CopySelectedId()
        {
            Entry entry = _viewModel.SelectedEntry;
            if (entry == null)
            {
                return;
            }

            string id = entry.Id.ToString("D").ToLowerInvariant();
            if (_viewModel.CopyText(id).Succeeded)
            {
                _viewModel.ReportStatus("已复制 Note ID：" + id, false);
            }
        }

        private void CopySelectedJson()
        {
            Entry entry = _viewModel.SelectedEntry;
            if (entry == null)
            {
                return;
            }

            if (_viewModel.CopyText(AgentNotePayload.Serialize(entry)).Succeeded)
            {
                _viewModel.ReportStatus("已复制智能体 JSON：《" + EntryTitle(entry) + "》", false);
            }
        }

        private void OpenSelectedSticky()
        {
            Entry entry = _viewModel.SelectedEntry;
            if (entry == null || entry.IsDeleted)
            {
                return;
            }
            if (entry.Sticky.Left == 0 && entry.Sticky.Top == 0)
            {
                entry.Sticky.Left = Math.Max(SystemParameters.VirtualScreenLeft, Left + ActualWidth - entry.Sticky.Width - 24);
                entry.Sticky.Top = Math.Max(SystemParameters.VirtualScreenTop, Top + 72);
            }
            ClampStickyBounds(entry);
            _stickyWindows.OpenOrActivate(entry);
            _viewModel.ReportStatus("已打开置顶小窗：《" + EntryTitle(entry) + "》", false);
        }

        private void SoftDeleteSelected()
        {
            Entry entry = _viewModel.SelectedEntry;
            if (entry == null)
            {
                return;
            }
            _stickyWindows.Close(entry.Id);
            _viewModel.SoftDeleteSelected();
        }

        private void PermanentlyDeleteSelected()
        {
            Entry entry = _viewModel.SelectedEntry;
            if (entry == null || !entry.IsDeleted)
            {
                return;
            }
            string title = EntryTitle(entry);
            MessageBoxResult answer = MessageBox.Show(
                this,
                "永久删除《" + title + "》？\n\n此操作无法撤销。历史备份中仍可能保留旧内容。",
                "确认永久删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer == MessageBoxResult.Yes)
            {
                _stickyWindows.Close(entry.Id);
                _viewModel.PermanentlyDeleteSelected();
            }
        }

        private void ClearTrashOnClick(object sender, RoutedEventArgs eventArgs)
        {
            int count = _viewModel.TrashCount;
            if (count == 0)
            {
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                this,
                "确定清空回收站中的全部 " + count + " 条内容？\n\n此操作无法撤销。历史备份中仍可能保留旧内容。",
                "确认清空回收站",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (Entry entry in _viewModel.State.Entries.Where(candidate => candidate != null && candidate.IsDeleted).ToList())
            {
                _stickyWindows.Close(entry.Id);
            }
            int removed = _viewModel.ClearTrash();
            _viewModel.ReportStatus("回收站已清空，共永久删除 " + removed + " 条。", false);
        }

        private void ExportOnClick(object sender, RoutedEventArgs eventArgs)
        {
            var dialog = new SaveFileDialog
            {
                Title = "导出 SeerNote 完整备份",
                Filter = "JSON 数据文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = "SeerNote-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json",
                AddExtension = true,
                DefaultExt = ".json"
            };
            if (dialog.ShowDialog(this) == true)
            {
                _viewModel.Export(dialog.FileName);
            }
        }

        private void SettingsOnClick(object sender, RoutedEventArgs eventArgs)
        {
            SettingsDialogSelection selection;
            if (!SettingsDialog.TryEdit(this, _viewModel.CloseButtonBehavior, _viewModel.AppTheme, out selection))
            {
                return;
            }

            _viewModel.UpdateCloseButtonBehavior(selection.CloseButtonBehavior);
            bool themeChanged = _viewModel.AppTheme != selection.Theme;
            _viewModel.UpdateAppTheme(selection.Theme);
            if (themeChanged)
            {
                ThemeResources.ApplyTheme(Application.Current.Resources, selection.Theme);
                EventHandler handler = ThemeChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            _viewModel.RequestImmediateSave();
        }

        private void ViewModelOnContentChanged(object sender, EventArgs eventArgs)
        {
            if (_editing)
            {
                _resultsRefreshTimer.Stop();
                _resultsRefreshTimer.Start();
                return;
            }
            RefreshAll();
        }

        private void ViewModelOnSelectedEntryChanged(object sender, EventArgs eventArgs)
        {
            _refreshing = true;
            try
            {
                Entry selectedEntry = _viewModel.SelectedEntry;
                if (!ReferenceEquals(_entryList.SelectedItem, selectedEntry))
                {
                    _entryList.SelectedItem = selectedEntry;
                    if (selectedEntry != null)
                    {
                        _entryList.ScrollIntoView(selectedEntry);
                    }
                }
                RefreshEditor();
                RefreshStatus();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void ViewModelOnStatusChanged(object sender, EventArgs eventArgs)
        {
            RefreshStatus();
        }

        private void ResultsRefreshTimerOnTick(object sender, EventArgs eventArgs)
        {
            _resultsRefreshTimer.Stop();
            _refreshing = true;
            try
            {
                RefreshResults();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void SearchBoxOnTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            bool hasQuery = _searchBox.Text.Length > 0;
            if (_searchPlaceholder != null)
            {
                _searchPlaceholder.Visibility = hasQuery ? Visibility.Collapsed : Visibility.Visible;
            }
            if (_searchShortcut != null)
            {
                _searchShortcut.Visibility = hasQuery ? Visibility.Collapsed : Visibility.Visible;
            }
            if (_clearSearchButton != null)
            {
                _clearSearchButton.Visibility = hasQuery ? Visibility.Visible : Visibility.Collapsed;
            }
            if (!_refreshing)
            {
                _viewModel.SetSearchText(_searchBox.Text);
            }
        }

        private void ClearSearchAndFocus()
        {
            _searchBox.Clear();
            _searchBox.Focus();
        }

        private void SearchBoxOnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.Down && _entryList.Items.Count > 0)
            {
                _entryList.SelectedIndex = Math.Max(0, _entryList.SelectedIndex);
                ((ListBoxItem)_entryList.ItemContainerGenerator.ContainerFromIndex(_entryList.SelectedIndex))?.Focus();
                eventArgs.Handled = true;
            }
            else if (eventArgs.Key == Key.Enter && _entryList.Items.Count > 0)
            {
                if (_entryList.SelectedIndex < 0)
                {
                    _entryList.SelectedIndex = 0;
                }
                _bodyBox.Focus();
                eventArgs.Handled = true;
            }
        }

        private void EntryListOnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }
            if (eventArgs.Key == Key.Enter && _entryList.SelectedItem != null)
            {
                _bodyBox.Focus();
                eventArgs.Handled = true;
            }
            else if (eventArgs.Key == Key.Escape && _searchBox.Text.Length > 0)
            {
                ClearSearchAndFocus();
                eventArgs.Handled = true;
            }
        }

        private void EntryListOnSelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            if (_refreshing)
            {
                return;
            }
            _viewModel.SelectEntry(_entryList.SelectedItem as Entry);
        }

        private void EntryListOnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs eventArgs)
        {
            var item = ItemsControl.ContainerFromElement(_entryList, eventArgs.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                _entryList.SelectedItem = _entryList.ItemContainerGenerator.ItemFromContainer(item) as Entry;
            }
        }

        private void EntryListOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
        {
            _entryDragStart = eventArgs.GetPosition(_entryList);
            var item = ItemsControl.ContainerFromElement(_entryList, eventArgs.OriginalSource as DependencyObject) as ListBoxItem;
            _entryDragEntry = item == null ? null : _entryList.ItemContainerGenerator.ItemFromContainer(item) as Entry;
        }

        private void EntryListOnPreviewMouseMove(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.LeftButton != MouseButtonState.Pressed || _entryDragEntry == null || _entryDragEntry.IsDeleted)
            {
                return;
            }
            Point current = eventArgs.GetPosition(_entryList);
            if (Math.Abs(current.X - _entryDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(current.Y - _entryDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            Entry entry = _entryDragEntry;
            _entryDragEntry = null;
            DragDrop.DoDragDrop(_entryList, new DataObject(CategorySidebar.EntryDragFormat, entry.Id.ToString("D")), DragDropEffects.Move);
        }

        private void CategoryCreateOnRequested(object sender, EventArgs eventArgs)
        {
            string category;
            if (!CategoryDialog.TryEdit(this, "新建分类", String.Empty, out category))
            {
                return;
            }
            string error;
            if (!_viewModel.CreateCategory(category, out error))
            {
                MessageBox.Show(this, error, "无法新建分类", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CategorySidebarOnCategorySelected(object sender, CategoryNameEventArgs eventArgs)
        {
            _viewModel.SelectCategory(eventArgs.Category);
        }

        private void CategoryRenameOnRequested(object sender, CategoryNameEventArgs eventArgs)
        {
            string category;
            if (!CategoryDialog.TryEdit(this, "重命名分类", eventArgs.Category, out category))
            {
                return;
            }
            string error;
            if (!_viewModel.RenameCategory(eventArgs.Category, category, out error))
            {
                MessageBox.Show(this, error, "无法重命名分类", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CategoryDeleteOnRequested(object sender, CategoryNameEventArgs eventArgs)
        {
            int count = _viewModel.State.Entries.Count(entry => entry != null
                && !entry.IsDeleted
                && String.Equals(entry.Category, eventArgs.Category, StringComparison.InvariantCultureIgnoreCase));
            MessageBoxResult result = MessageBox.Show(
                this,
                "删除分类“" + eventArgs.Category + "”？\n\n其中的 " + count + " 条 Note 会保留并回到“未分类”。",
                "确认删除分类",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.DeleteCategory(eventArgs.Category);
            }
        }

        private void CategoryReorderOnRequested(object sender, CategoryReorderEventArgs eventArgs)
        {
            _viewModel.ReorderCategory(eventArgs.Category, eventArgs.TargetCategory, eventArgs.InsertAfter);
        }

        private void EntryCategoryMoveOnRequested(object sender, EntryCategoryMoveEventArgs eventArgs)
        {
            if (_viewModel.MoveEntryToCategory(eventArgs.EntryId, eventArgs.Category))
            {
                _viewModel.ReportStatus("已移动到“" + eventArgs.Category + "”。", false);
            }
        }

        private void TitleBoxOnTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            Edit(delegate { _viewModel.UpdateSelectedTitle(_titleBox.Text); });
        }

        private void BodyBoxOnTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            Edit(delegate { _viewModel.UpdateSelectedBody(_bodyBox.Text); });
        }

        private void CategoryBoxOnSelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            if (!_refreshing && _categoryBox.SelectedIndex >= 0 && _viewModel.SelectedEntry != null)
            {
                string category = _categoryBox.SelectedIndex == 0 ? null : _categoryBox.SelectedItem as string;
                if (!String.Equals(_viewModel.SelectedEntry.Category, category ?? String.Empty, StringComparison.InvariantCultureIgnoreCase))
                {
                    _viewModel.MoveEntryToCategory(_viewModel.SelectedEntry.Id, category);
                }
            }
        }

        private void Edit(Action action)
        {
            if (_refreshing)
            {
                return;
            }
            _editing = true;
            try
            {
                action();
            }
            finally
            {
                _editing = false;
            }
        }

        private void WindowBoundsOnChanged(object sender, EventArgs eventArgs)
        {
            if (!_initializingBounds && WindowState == WindowState.Normal && IsLoaded)
            {
                SaveCurrentBounds();
            }
        }

        private void MainGridOnSizeChanged(object sender, SizeChangedEventArgs eventArgs)
        {
            ApplyResponsiveLayout(eventArgs.NewSize.Width);
        }

        private void ApplyResponsiveLayout(double clientWidth)
        {
            if (_sidebarColumn == null || _listColumn == null || _editorColumn == null)
            {
                return;
            }
            MainWindowLayout layout = MainWindowLayoutCalculator.GetLayout(clientWidth);
            _sidebarColumn.Width = new GridLength(layout.SidebarWidth);
            _sidebarColumn.MinWidth = layout.SidebarWidth;
            _listColumn.Width = new GridLength(layout.ListWidth);
            _listColumn.MinWidth = layout.ListWidth;
            _editorColumn.MinWidth = layout.EditorMinimumWidth;
        }

        private void MainWindowOnLoaded(object sender, RoutedEventArgs eventArgs)
        {
            foreach (Entry entry in _viewModel.State.Entries.Where(candidate => candidate != null && !candidate.IsDeleted && candidate.Sticky != null && candidate.Sticky.IsOpen).ToList())
            {
                ClampStickyBounds(entry);
                _stickyWindows.OpenOrActivate(entry);
            }
            RefreshStatus();
            Dispatcher.BeginInvoke(new Action(delegate { _searchBox.Focus(); }), DispatcherPriority.Input);
        }

        private void MainWindowOnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;
            if (modifiers == ModifierKeys.Control && eventArgs.Key == Key.N)
            {
                CreateAndEdit();
                eventArgs.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && eventArgs.Key == Key.F)
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                eventArgs.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && eventArgs.Key == Key.S)
            {
                _viewModel.RequestImmediateSave();
                eventArgs.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && eventArgs.Key == Key.Enter)
            {
                CopySelected();
                eventArgs.Handled = true;
            }
            else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && eventArgs.Key == Key.P)
            {
                _viewModel.ToggleFavorite();
                eventArgs.Handled = true;
            }
            else if (modifiers == ModifierKeys.None && eventArgs.Key == Key.Delete && _entryList.IsKeyboardFocusWithin)
            {
                if (_viewModel.SelectedView == SmartView.Trash)
                {
                    PermanentlyDeleteSelected();
                }
                else
                {
                    SoftDeleteSelected();
                }
                eventArgs.Handled = true;
            }
            else if (modifiers == ModifierKeys.None && eventArgs.Key == Key.Escape && _searchBox.IsKeyboardFocusWithin && _searchBox.Text.Length > 0)
            {
                ClearSearchAndFocus();
                eventArgs.Handled = true;
            }
        }

        private void OnStickyEntryChanged(Entry entry)
        {
            _viewModel.NotifyExternalEntryChanged(entry);
        }

        private void AddViewButton(Panel parent, SmartView view, string text, string help)
        {
            Button button = QuietButton(text, help);
            button.Tag = view;
            button.Style = (Style)FindResource("Seer.NavigationButton");
            button.Margin = new Thickness(0, 2, 0, 0);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var count = new TextBlock
            {
                Text = "0",
                FontSize = 10.5,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(count, 1);
            row.Children.Add(count);
            button.Content = row;
            button.Click += delegate { _viewModel.SelectView((SmartView)button.Tag); };
            _viewButtons.Add(view, button);
            _viewCounts.Add(view, count);
            parent.Children.Add(button);
        }

        private Button QuietButton(string text, string help)
        {
            var button = new Button { Content = text, Style = (Style)FindResource("Seer.QuietButton") };
            AutomationProperties.SetName(button, text);
            AutomationProperties.SetHelpText(button, help);
            return button;
        }

        private TextBlock SectionLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                Margin = new Thickness(3, 12, 0, 6)
            };
        }

        private string CurrentScopeLabel()
        {
            if (!String.IsNullOrWhiteSpace(_viewModel.SelectedCategory))
            {
                return _viewModel.SelectedCategory.Trim();
            }
            switch (_viewModel.SelectedView)
            {
                case SmartView.Favorite:
                    return "收藏 Note";
                case SmartView.Trash:
                    return "回收站";
                default:
                    return "全部 Note";
            }
        }

        private static string GetDisplayVersion()
        {
            Version version = typeof(MainWindow).Assembly.GetName().Version;
            return version == null
                ? "v0.0.0"
                : "v" + version.Major + "." + version.Minor + "." + version.Build;
        }

        private Border SeparatorAtColumn(int column)
        {
            var separator = new Border { Background = Brush(ThemeResources.BorderBrushKey) };
            Grid.SetColumn(separator, column);
            return separator;
        }

        private Brush Brush(string key)
        {
            return (Brush)FindResource(key);
        }

        private void ApplyStoredBounds()
        {
            WindowBounds bounds = _viewModel.State.Settings == null ? null : _viewModel.State.Settings.WindowBounds;
            if (bounds == null)
            {
                Size startupSize = MainWindowLayoutCalculator.GetStartupSize(SystemParameters.WorkArea.Size);
                CenterOnWorkArea(startupSize.Width, startupSize.Height);
                return;
            }
            double width = Math.Max(MinWidth, Math.Min(bounds.Width, SystemParameters.VirtualScreenWidth));
            double height = Math.Max(MinHeight, Math.Min(bounds.Height, SystemParameters.VirtualScreenHeight));
            double left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(bounds.Left, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 120));
            double top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(bounds.Top, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80));
            Width = width;
            Height = height;
            Left = left;
            Top = top;
        }

        private void CenterOnWorkArea(double width, double height)
        {
            Rect work = SystemParameters.WorkArea;
            Width = Math.Min(width, work.Width);
            Height = Math.Min(height, work.Height);
            Left = work.Left + Math.Max(0, (work.Width - Width) / 2);
            Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
        }

        private static void ClampStickyBounds(Entry entry)
        {
            StickyState state = entry.Sticky;
            state.Width = Math.Max(240, Math.Min(state.Width, SystemParameters.VirtualScreenWidth));
            state.Height = Math.Max(160, Math.Min(state.Height, SystemParameters.VirtualScreenHeight));
            state.Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(state.Left, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100));
            state.Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(state.Top, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60));
        }

        private static void SetTextPreservingCaret(TextBox box, string value)
        {
            if (String.Equals(box.Text, value, StringComparison.Ordinal))
            {
                return;
            }
            int caret = box.CaretIndex;
            box.Text = value;
            box.CaretIndex = Math.Min(caret, box.Text.Length);
        }

        private static string EntryTitle(Entry entry)
        {
            return EntryListRow.TitleFor(entry);
        }

        private static string Truncate(string value, int length)
        {
            value = value ?? String.Empty;
            return value.Length <= length ? value : value.Substring(0, Math.Max(0, length - 1)) + "…";
        }

        private void RaiseExitRequested()
        {
            EventHandler handler = ExitRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
