using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    /// <summary>
    /// Collects placeholder values from a Note without retaining them after the dialog closes.
    /// The supplied variable sequence determines visual and keyboard order.
    /// </summary>
    public sealed class VariableDialog : Window
    {
        private readonly IReadOnlyList<string> _variables;
        private readonly IDictionary<string, TextBox> _inputByVariable;
        private IReadOnlyDictionary<string, string> _values;

        public VariableDialog(IEnumerable<string> variables)
        {
            if (variables == null)
            {
                throw new ArgumentNullException(nameof(variables));
            }

            _variables = new ReadOnlyCollection<string>(variables.Where(variable => !String.IsNullOrWhiteSpace(variable)).ToList());
            _inputByVariable = new Dictionary<string, TextBox>(StringComparer.Ordinal);
            _values = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

            Title = "填写变量";
            Width = 420;
            MinWidth = 320;
            MaxHeight = SystemParameters.WorkArea.Height * 0.85;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = (System.Windows.Media.Brush)Application.Current.FindResource(ThemeResources.CanvasBrushKey);
            AutomationProperties.SetName(this, "填写变量");

            Content = BuildContent();
        }

        public IReadOnlyList<string> Variables
        {
            get { return _variables; }
        }

        public IReadOnlyDictionary<string, string> Values
        {
            get { return _values; }
        }

        public static bool TryCollect(Window owner, IEnumerable<string> variables, out IReadOnlyDictionary<string, string> values)
        {
            var dialog = new VariableDialog(variables) { Owner = owner };
            var accepted = dialog.ShowDialog() == true;
            values = accepted ? dialog.Values : null;
            return accepted;
        }

        private UIElement BuildContent()
        {
            var root = new DockPanel { Margin = new Thickness(18) };
            root.SetValue(TextElement.ForegroundProperty, Application.Current.FindResource(ThemeResources.InkBrushKey));
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var cancel = new Button { Content = "取消", IsCancel = true, MinWidth = 76, Margin = new Thickness(0, 0, 8, 0) };
            AutomationProperties.SetName(cancel, "取消填写变量");
            var confirm = new Button { Content = "复制", IsDefault = true, MinWidth = 76, Style = (Style)Application.Current.FindResource("Seer.PrimaryButton") };
            AutomationProperties.SetName(confirm, "确认变量并复制");
            confirm.Click += ConfirmOnClick;
            actions.Children.Add(cancel);
            actions.Children.Add(confirm);
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var content = new StackPanel();
            var instruction = new TextBlock
            {
                Text = _variables.Count == 0 ? "此 Note 没有需要填写的变量。" : "填写后将只用于本次复制，不会保存。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            content.Children.Add(instruction);

            foreach (var variable in _variables)
            {
                var label = new TextBlock { Text = "变量：" + variable, Margin = new Thickness(0, 8, 0, 4) };
                AutomationProperties.SetName(label, "变量标签 " + variable);
                var input = new TextBox { MinWidth = 240 };
                AutomationProperties.SetName(input, "变量 " + variable);
                AutomationProperties.SetHelpText(input, "填写“" + variable + "”的值；此值不会保存。");
                _inputByVariable.Add(variable, input);
                content.Children.Add(label);
                content.Children.Add(input);
            }

            scroll.Content = content;
            root.Children.Add(scroll);
            Loaded += delegate
            {
                var firstInput = _inputByVariable.Values.FirstOrDefault();
                if (firstInput != null)
                {
                    firstInput.Focus();
                }
                else
                {
                    confirm.Focus();
                }
            };
            return root;
        }

        private void ConfirmOnClick(object sender, RoutedEventArgs eventArgs)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var variable in _variables)
            {
                values.Add(variable, _inputByVariable[variable].Text ?? String.Empty);
            }

            _values = new ReadOnlyDictionary<string, string>(values);
            DialogResult = true;
        }
    }
}
