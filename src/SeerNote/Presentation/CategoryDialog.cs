using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    public sealed class CategoryDialog : Window
    {
        private readonly TextBox _nameBox;
        private readonly TextBlock _errorText;

        private CategoryDialog(string title, string initialName)
        {
            Title = title;
            Width = 360;
            Height = 205;
            MinWidth = 320;
            MinHeight = 190;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = Brush(ThemeResources.CanvasBrushKey);
            AutomationProperties.SetName(this, "SeerNote 分类编辑");

            var root = new Grid { Margin = new Thickness(18) };
            root.SetValue(TextElement.ForegroundProperty, Brush(ThemeResources.InkBrushKey));
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = "分类名称",
                FontSize = 12,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                Margin = new Thickness(0, 0, 0, 6)
            });
            _nameBox = new TextBox
            {
                Text = initialName ?? String.Empty,
                MaxLength = 60,
                Height = 36,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(_nameBox, "分类名称");
            Grid.SetRow(_nameBox, 1);
            root.Children.Add(_nameBox);

            _errorText = new TextBlock
            {
                Foreground = Brush(ThemeResources.DangerBrushKey),
                Margin = new Thickness(0, 5, 0, 0),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_errorText, 2);
            root.Children.Add(_errorText);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancel = new Button { Content = "取消", IsCancel = true, MinWidth = 76, Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button
            {
                Content = "保存",
                IsDefault = true,
                MinWidth = 84,
                Style = (Style)FindResource("Seer.PrimaryButton")
            };
            save.Click += SaveOnClick;
            actions.Children.Add(cancel);
            actions.Children.Add(save);
            Grid.SetRow(actions, 4);
            root.Children.Add(actions);
            Content = root;

            Loaded += delegate
            {
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
        }

        public string CategoryName { get; private set; }

        public static bool TryEdit(Window owner, string title, string initialName, out string categoryName)
        {
            var dialog = new CategoryDialog(title, initialName) { Owner = owner };
            bool accepted = dialog.ShowDialog() == true;
            categoryName = accepted ? dialog.CategoryName : null;
            return accepted;
        }

        private void SaveOnClick(object sender, RoutedEventArgs eventArgs)
        {
            string normalized = String.IsNullOrWhiteSpace(_nameBox.Text) ? null : _nameBox.Text.Trim();
            if (normalized == null)
            {
                _errorText.Text = "请输入分类名称。";
                _errorText.Visibility = Visibility.Visible;
                _nameBox.Focus();
                return;
            }
            CategoryName = normalized;
            DialogResult = true;
        }

        private static Brush Brush(string key)
        {
            return (Brush)Application.Current.FindResource(key);
        }
    }
}
