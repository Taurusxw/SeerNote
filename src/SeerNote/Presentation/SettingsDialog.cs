using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SeerNote.Domain;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    public sealed class SettingsDialogSelection
    {
        public SettingsDialogSelection(CloseButtonBehavior closeButtonBehavior, AppTheme theme)
        {
            CloseButtonBehavior = closeButtonBehavior;
            Theme = theme;
        }

        public CloseButtonBehavior CloseButtonBehavior { get; private set; }

        public AppTheme Theme { get; private set; }
    }

    public sealed class SettingsDialog : Window
    {
        private readonly RadioButton _exitOption;
        private readonly RadioButton _trayOption;
        private readonly IDictionary<AppTheme, RadioButton> _themeOptions;
        private Expander _themeSection;
        private Expander _closeBehaviorSection;
        private TextBlock _themeSectionSummary;
        private TextBlock _closeBehaviorSectionSummary;

        public SettingsDialog(CloseButtonBehavior currentBehavior, AppTheme currentTheme)
        {
            if (!Enum.IsDefined(typeof(CloseButtonBehavior), currentBehavior))
            {
                throw new ArgumentOutOfRangeException(nameof(currentBehavior));
            }
            if (!Enum.IsDefined(typeof(AppTheme), currentTheme))
            {
                throw new ArgumentOutOfRangeException(nameof(currentTheme));
            }

            SelectedCloseButtonBehavior = currentBehavior;
            SelectedTheme = currentTheme;
            Title = "设置";
            Width = 440;
            Height = Math.Max(420, Math.Min(480, SystemParameters.WorkArea.Height * 0.72));
            MinWidth = 380;
            MinHeight = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = (Brush)Application.Current.FindResource(ThemeResources.CanvasBrushKey);
            AutomationProperties.SetName(this, "SeerNote 设置");

            _themeOptions = new Dictionary<AppTheme, RadioButton>
            {
                { AppTheme.Graphite, CreateOption("石墨深色", "中性炭黑与青绿色强调，延续 SeerNote 当前风格。", "主题设为石墨深色", "AppTheme") },
                { AppTheme.Midnight, CreateOption("午夜蓝", "深蓝层级与柔和蓝色强调，适合低光环境。", "主题设为午夜蓝", "AppTheme") },
                { AppTheme.Porcelain, CreateOption("Win11 雾白", "参考 Windows 11 记事本的浅灰层级、白色画布与克制工具栏。", "主题设为 Win11 雾白", "AppTheme") },
                { AppTheme.Sage, CreateOption("鼠尾草", "低饱和浅绿与深绿强调，呼应参考图但保持克制。", "主题设为鼠尾草", "AppTheme") }
            };
            _themeOptions[currentTheme].IsChecked = true;

            _exitOption = CreateOption(
                "彻底退出",
                "关闭主窗口后保存内容、释放托盘和快捷键，并结束 SeerNote 进程。",
                "关闭按钮设为彻底退出",
                "CloseButtonBehavior");
            _trayOption = CreateOption(
                "最小化到托盘",
                "关闭主窗口后继续在后台运行，可从托盘或全局快捷键再次打开。",
                "关闭按钮设为最小化到托盘",
                "CloseButtonBehavior");
            _exitOption.IsChecked = currentBehavior == CloseButtonBehavior.Exit;
            _trayOption.IsChecked = currentBehavior == CloseButtonBehavior.MinimizeToTray;

            Content = BuildContent();
            foreach (KeyValuePair<AppTheme, RadioButton> pair in _themeOptions)
            {
                pair.Value.Checked += delegate { UpdateThemeSectionSummary(); };
            }
            _exitOption.Checked += delegate { UpdateCloseBehaviorSectionSummary(); };
            _trayOption.Checked += delegate { UpdateCloseBehaviorSectionSummary(); };
            UpdateThemeSectionSummary();
            UpdateCloseBehaviorSectionSummary();
            Loaded += delegate
            {
                _themeSection.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            };
        }

        public CloseButtonBehavior SelectedCloseButtonBehavior { get; private set; }

        public AppTheme SelectedTheme { get; private set; }

        public static bool TryEdit(
            Window owner,
            CloseButtonBehavior currentBehavior,
            AppTheme currentTheme,
            out SettingsDialogSelection selection)
        {
            var dialog = new SettingsDialog(currentBehavior, currentTheme) { Owner = owner };
            bool accepted = dialog.ShowDialog() == true;
            selection = accepted
                ? new SettingsDialogSelection(dialog.SelectedCloseButtonBehavior, dialog.SelectedTheme)
                : null;
            return accepted;
        }

        private UIElement BuildContent()
        {
            var root = new DockPanel { Margin = new Thickness(20) };
            root.SetValue(TextElement.ForegroundProperty, Application.Current.FindResource(ThemeResources.InkBrushKey));

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            var cancel = new Button { Content = "取消", IsCancel = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
            AutomationProperties.SetName(cancel, "取消设置");
            var save = new Button
            {
                Content = "保存设置",
                IsDefault = true,
                MinWidth = 96,
                Style = (Style)Application.Current.FindResource("Seer.PrimaryButton")
            };
            AutomationProperties.SetName(save, "保存设置");
            save.Click += SaveOnClick;
            actions.Children.Add(cancel);
            actions.Children.Add(save);
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);

            var content = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            var themeOptions = new StackPanel();
            AddOption(themeOptions, _themeOptions[AppTheme.Graphite], "中性炭黑，当前经典风格。");
            AddOption(themeOptions, _themeOptions[AppTheme.Midnight], "深蓝低眩光，适合夜间。");
            AddOption(themeOptions, _themeOptions[AppTheme.Porcelain], "浅灰标签栏与纯白编辑画布，接近 Win11 记事本的阅读感。");
            AddOption(themeOptions, _themeOptions[AppTheme.Sage], "柔和浅绿，接近参考图的自然感。");
            content.Children.Add(CreateSection(
                "ThemeSettingsSection",
                "主题风格",
                "四种克制配色共用同一套轻量界面；保存后立即生效，无需重启。",
                themeOptions,
                out _themeSection,
                out _themeSectionSummary));

            var closeBehaviorOptions = new StackPanel();
            closeBehaviorOptions.Children.Add(_exitOption);
            closeBehaviorOptions.Children.Add(CreateDescription("关闭后任务管理器中不会保留 SeerNote。"));
            closeBehaviorOptions.Children.Add(_trayOption);
            closeBehaviorOptions.Children.Add(CreateDescription("后台驻留期间仍会占用一个 SeerNote 进程。"));
            content.Children.Add(CreateSection(
                "CloseBehaviorSettingsSection",
                "关闭按钮行为",
                "选择点击主窗口右上角关闭按钮时 SeerNote 应执行的操作。",
                closeBehaviorOptions,
                out _closeBehaviorSection,
                out _closeBehaviorSectionSummary));
            var scroll = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            root.Children.Add(scroll);
            return root;
        }

        private Border CreateSection(
            string automationId,
            string title,
            string description,
            UIElement options,
            out Expander section,
            out TextBlock summary)
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            summary = new TextBlock
            {
                Foreground = (Brush)Application.Current.FindResource(ThemeResources.MutedBrushKey),
                FontSize = 12,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(summary, 1);
            header.Children.Add(titleText);
            header.Children.Add(summary);

            var body = new StackPanel { Margin = new Thickness(22, 6, 0, 0) };
            body.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.FindResource(ThemeResources.MutedBrushKey),
                Margin = new Thickness(0, 0, 0, 4)
            });
            body.Children.Add(options);

            section = new Expander
            {
                Name = automationId,
                Header = header,
                Content = body,
                IsExpanded = false,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetAutomationId(section, automationId);
            AutomationProperties.SetHelpText(section, description);

            return new Border
            {
                Background = (Brush)Application.Current.FindResource(ThemeResources.SurfaceBrushKey),
                BorderBrush = (Brush)Application.Current.FindResource(ThemeResources.BorderBrushKey),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10),
                Child = section
            };
        }

        private void UpdateThemeSectionSummary()
        {
            if (_themeSectionSummary == null)
            {
                return;
            }
            string label = ThemeLabel(CheckedTheme());
            _themeSectionSummary.Text = "当前：" + label;
            AutomationProperties.SetName(_themeSection, "主题风格，当前：" + label);
        }

        private void UpdateCloseBehaviorSectionSummary()
        {
            if (_closeBehaviorSectionSummary == null)
            {
                return;
            }
            string label = _trayOption.IsChecked == true ? "最小化到托盘" : "彻底退出";
            _closeBehaviorSectionSummary.Text = "当前：" + label;
            AutomationProperties.SetName(_closeBehaviorSection, "关闭按钮行为，当前：" + label);
        }

        private AppTheme CheckedTheme()
        {
            foreach (KeyValuePair<AppTheme, RadioButton> pair in _themeOptions)
            {
                if (pair.Value.IsChecked == true)
                {
                    return pair.Key;
                }
            }
            return SelectedTheme;
        }

        private static string ThemeLabel(AppTheme theme)
        {
            switch (theme)
            {
                case AppTheme.Graphite:
                    return "石墨深色";
                case AppTheme.Midnight:
                    return "午夜蓝";
                case AppTheme.Porcelain:
                    return "Win11 雾白";
                case AppTheme.Sage:
                    return "鼠尾草";
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        private RadioButton CreateOption(string text, string helpText, string automationName, string groupName)
        {
            var option = new RadioButton
            {
                Content = text,
                GroupName = groupName,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource(ThemeResources.InkBrushKey),
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(2)
            };
            AutomationProperties.SetName(option, automationName);
            AutomationProperties.SetHelpText(option, helpText);
            return option;
        }

        private void AddOption(Panel panel, RadioButton option, string description)
        {
            panel.Children.Add(option);
            panel.Children.Add(CreateDescription(description));
        }

        private TextBlock CreateDescription(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.FindResource(ThemeResources.MutedBrushKey),
                Margin = new Thickness(24, 4, 0, 8)
            };
        }

        private void SaveOnClick(object sender, RoutedEventArgs eventArgs)
        {
            SelectedCloseButtonBehavior = _trayOption.IsChecked == true
                ? CloseButtonBehavior.MinimizeToTray
                : CloseButtonBehavior.Exit;
            SelectedTheme = CheckedTheme();
            DialogResult = true;
        }
    }
}
