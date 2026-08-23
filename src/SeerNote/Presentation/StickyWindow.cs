using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using SeerNote.Domain;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    /// <summary>
    /// An editable, independent view over one in-memory entry. Closing it only changes
    /// Sticky.IsOpen; it never deletes the entry.
    /// </summary>
    public sealed class StickyWindow : Window
    {
        private readonly TextBox _editor;
        private readonly DispatcherTimer _adaptiveSizeTimer;
        private bool _initializingBounds;
        private bool _applyingAdaptiveSize;
        private bool _acceptManualResize;
        private bool _manualSizeOverride;
        private bool _isClosed;

        public StickyWindow(Entry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            if (Entry.Sticky == null)
            {
                Entry.Sticky = new StickyState();
            }
            _adaptiveSizeTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _adaptiveSizeTimer.Tick += AdaptiveSizeTimerOnTick;

            Title = GetTitle();
            MinWidth = StickyWindowSizeCalculator.MinimumWidth;
            MinHeight = StickyWindowSizeCalculator.MinimumHeight;
            Topmost = true;
            ShowInTaskbar = true;
            Background = (System.Windows.Media.Brush)Application.Current.FindResource(ThemeResources.CanvasBrushKey);
            AutomationProperties.SetName(this, "置顶小窗：" + GetTitle());

            _editor = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Text = Entry.Body ?? String.Empty,
                Margin = new Thickness(10)
            };
            AutomationProperties.SetName(_editor, "便签正文");
            AutomationProperties.SetHelpText(_editor, "可直接编辑当前便签正文。");
            _editor.TextChanged += EditorOnTextChanged;
            Content = _editor;

            _initializingBounds = true;
            ApplyStoredPosition();
            ApplyAdaptiveSize();
            _initializingBounds = false;
            Entry.Sticky.IsOpen = true;
            LocationChanged += BoundsOnChanged;
            SizeChanged += BoundsOnChanged;
            Closed += ClosedOnClosed;
            Loaded += WindowOnLoaded;
        }

        public Entry Entry { get; private set; }

        public event Action<Entry> EntryChanged;

        public event Action<StickyWindow> WindowClosed;

        private void EditorOnTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            if (Entry.Body == _editor.Text)
            {
                return;
            }

            Entry.Body = _editor.Text;
            Entry.UpdatedUtc = DateTime.UtcNow;
            Title = GetTitle();
            AutomationProperties.SetName(this, "置顶小窗：" + GetTitle());
            NotifyEntryChanged();
            QueueAdaptiveSize();
        }

        private void WindowOnLoaded(object sender, RoutedEventArgs eventArgs)
        {
            ApplyAdaptiveSize();
            _editor.Focus();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate
            {
                if (!_isClosed)
                {
                    _acceptManualResize = true;
                }
            }));
        }

        private void BoundsOnChanged(object sender, EventArgs eventArgs)
        {
            if (eventArgs is SizeChangedEventArgs && _acceptManualResize && !_applyingAdaptiveSize)
            {
                _manualSizeOverride = true;
            }
            if (_initializingBounds || _applyingAdaptiveSize || _isClosed || !IsFinite(Left) || !IsFinite(Top) || !IsFinite(Width) || !IsFinite(Height))
            {
                return;
            }

            Entry.Sticky.Left = Left;
            Entry.Sticky.Top = Top;
            Entry.Sticky.Width = Width;
            Entry.Sticky.Height = Height;
            NotifyEntryChanged();
        }

        private void ClosedOnClosed(object sender, EventArgs eventArgs)
        {
            _isClosed = true;
            _adaptiveSizeTimer.Stop();
            Entry.Sticky.IsOpen = false;
            NotifyEntryChanged();
            var closed = WindowClosed;
            if (closed != null)
            {
                closed(this);
            }
        }

        private void ApplyStoredPosition()
        {
            var state = Entry.Sticky;
            Left = IsFinite(state.Left) ? state.Left : 0;
            Top = IsFinite(state.Top) ? state.Top : 0;
        }

        private void QueueAdaptiveSize()
        {
            if (_manualSizeOverride || _isClosed)
            {
                return;
            }
            _adaptiveSizeTimer.Stop();
            _adaptiveSizeTimer.Start();
        }

        private void AdaptiveSizeTimerOnTick(object sender, EventArgs eventArgs)
        {
            _adaptiveSizeTimer.Stop();
            if (!_manualSizeOverride && !_isClosed)
            {
                ApplyAdaptiveSize();
            }
        }

        private void ApplyAdaptiveSize()
        {
            Size workAreaSize = SystemParameters.WorkArea.Size;
            Size maximum = StickyWindowSizeCalculator.GetMaximumSize(workAreaSize);
            Size target = StickyWindowSizeCalculator.Calculate(GetTitle(), _editor.Text, workAreaSize);

            _applyingAdaptiveSize = true;
            try
            {
                MaxWidth = maximum.Width;
                MaxHeight = maximum.Height;
                Width = target.Width;
                Height = target.Height;
                KeepInsideVirtualScreen();
                Entry.Sticky.Left = Left;
                Entry.Sticky.Top = Top;
                Entry.Sticky.Width = Width;
                Entry.Sticky.Height = Height;
            }
            finally
            {
                _applyingAdaptiveSize = false;
            }

            if (!_initializingBounds)
            {
                NotifyEntryChanged();
            }
        }

        private void KeepInsideVirtualScreen()
        {
            double screenLeft = SystemParameters.VirtualScreenLeft;
            double screenTop = SystemParameters.VirtualScreenTop;
            double screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
            double screenBottom = screenTop + SystemParameters.VirtualScreenHeight;
            Left = Math.Max(screenLeft, Math.Min(Left, screenRight - Width));
            Top = Math.Max(screenTop, Math.Min(Top, screenBottom - Height));
        }

        private string GetTitle()
        {
            var title = Entry.DisplayTitle;
            return String.IsNullOrWhiteSpace(title) ? "未命名便签" : title;
        }

        private void NotifyEntryChanged()
        {
            var changed = EntryChanged;
            if (changed != null)
            {
                changed(Entry);
            }
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }
}
