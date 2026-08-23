using System;
using System.Drawing;
using System.Windows.Forms;

namespace SeerNote.Platform
{
    public sealed class TrayMenuTheme
    {
        private TrayMenuTheme()
        {
            UseSystemRenderer = true;
        }

        public TrayMenuTheme(Color background, Color foreground, Color border, Color selectionBackground, Color selectionBorder)
        {
            Background = background;
            Foreground = foreground;
            Border = border;
            SelectionBackground = selectionBackground;
            SelectionBorder = selectionBorder;
        }

        public static TrayMenuTheme SystemDefault
        {
            get { return new TrayMenuTheme(); }
        }

        public bool UseSystemRenderer { get; private set; }
        public Color Background { get; private set; }
        public Color Foreground { get; private set; }
        public Color Border { get; private set; }
        public Color SelectionBackground { get; private set; }
        public Color SelectionBorder { get; private set; }
    }

    public sealed class TrayMenuLabels
    {
        public TrayMenuLabels(string show, string create, string exit)
        {
            Show = RequireText(show, nameof(show));
            Create = RequireText(create, nameof(create));
            Exit = RequireText(exit, nameof(exit));
        }

        public string Show { get; private set; }

        public string Create { get; private set; }

        public string Exit { get; private set; }

        private static string RequireText(string text, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Menu text is required.", parameterName);
            }

            return text;
        }
    }

    /// <summary>
    /// Owns the notification-area icon and exposes its commands without owning application behavior.
    /// </summary>
    public sealed class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly Icon _ownedIcon;
        private bool _disposed;

        public TrayIconService(Icon icon, string toolTip, TrayMenuLabels labels)
        {
            if (icon == null)
            {
                throw new ArgumentNullException(nameof(icon));
            }

            if (labels == null)
            {
                throw new ArgumentNullException(nameof(labels));
            }

            _ownedIcon = (Icon)icon.Clone();
            _menu = new ContextMenuStrip();
            _menu.Items.Add(labels.Show, null, OnShowClicked);
            _menu.Items.Add(labels.Create, null, OnCreateClicked);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(labels.Exit, null, OnExitClicked);

            _notifyIcon = new NotifyIcon
            {
                Icon = _ownedIcon,
                Text = NormalizeToolTip(toolTip),
                ContextMenuStrip = _menu,
                Visible = false
            };
            _notifyIcon.DoubleClick += OnShowRequested;
        }

        public event EventHandler ShowRequested;

        public event EventHandler NewRequested;

        public event EventHandler ExitRequested;

        public bool IsVisible
        {
            get { return _notifyIcon.Visible; }
        }

        public void Show()
        {
            ThrowIfDisposed();
            _notifyIcon.Visible = true;
        }

        public void Hide()
        {
            if (!_disposed)
            {
                _notifyIcon.Visible = false;
            }
        }

        public void ApplyTheme(TrayMenuTheme theme)
        {
            ThrowIfDisposed();
            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            if (theme.UseSystemRenderer)
            {
                _menu.RenderMode = ToolStripRenderMode.System;
                _menu.BackColor = SystemColors.Menu;
                _menu.ForeColor = SystemColors.MenuText;
                ApplyItemColors(_menu.Items, SystemColors.Menu, SystemColors.MenuText);
                return;
            }

            var renderer = new ToolStripProfessionalRenderer(new TrayMenuColorTable(theme))
            {
                RoundedEdges = false
            };
            _menu.Renderer = renderer;
            _menu.BackColor = theme.Background;
            _menu.ForeColor = theme.Foreground;
            ApplyItemColors(_menu.Items, theme.Background, theme.Foreground);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _notifyIcon.DoubleClick -= OnShowRequested;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _ownedIcon.Dispose();
            ShowRequested = null;
            NewRequested = null;
            ExitRequested = null;
        }

        private void OnShowClicked(object sender, EventArgs eventArgs)
        {
            Raise(ShowRequested);
        }

        private void OnCreateClicked(object sender, EventArgs eventArgs)
        {
            Raise(NewRequested);
        }

        private void OnExitClicked(object sender, EventArgs eventArgs)
        {
            Raise(ExitRequested);
        }

        private void OnShowRequested(object sender, EventArgs eventArgs)
        {
            Raise(ShowRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (!_disposed && handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static string NormalizeToolTip(string toolTip)
        {
            if (string.IsNullOrWhiteSpace(toolTip))
            {
                throw new ArgumentException("A tooltip is required.", nameof(toolTip));
            }

            return toolTip.Length <= 63 ? toolTip : toolTip.Substring(0, 63);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TrayIconService));
            }
        }

        private static void ApplyItemColors(ToolStripItemCollection items, Color background, Color foreground)
        {
            foreach (ToolStripItem item in items)
            {
                item.BackColor = background;
                item.ForeColor = foreground;
                var menuItem = item as ToolStripMenuItem;
                if (menuItem != null && menuItem.DropDownItems.Count > 0)
                {
                    menuItem.DropDown.BackColor = background;
                    menuItem.DropDown.ForeColor = foreground;
                    ApplyItemColors(menuItem.DropDownItems, background, foreground);
                }
            }
        }

        private sealed class TrayMenuColorTable : ProfessionalColorTable
        {
            private readonly TrayMenuTheme _theme;

            public TrayMenuColorTable(TrayMenuTheme theme)
            {
                _theme = theme;
                UseSystemColors = false;
            }

            public override Color ToolStripDropDownBackground { get { return _theme.Background; } }
            public override Color ImageMarginGradientBegin { get { return _theme.Background; } }
            public override Color ImageMarginGradientMiddle { get { return _theme.Background; } }
            public override Color ImageMarginGradientEnd { get { return _theme.Background; } }
            public override Color MenuBorder { get { return _theme.Border; } }
            public override Color MenuItemBorder { get { return _theme.SelectionBorder; } }
            public override Color MenuItemSelected { get { return _theme.SelectionBackground; } }
            public override Color MenuItemSelectedGradientBegin { get { return _theme.SelectionBackground; } }
            public override Color MenuItemSelectedGradientEnd { get { return _theme.SelectionBackground; } }
            public override Color MenuItemPressedGradientBegin { get { return _theme.SelectionBackground; } }
            public override Color MenuItemPressedGradientMiddle { get { return _theme.SelectionBackground; } }
            public override Color MenuItemPressedGradientEnd { get { return _theme.SelectionBackground; } }
            public override Color CheckBackground { get { return _theme.SelectionBackground; } }
            public override Color CheckSelectedBackground { get { return _theme.SelectionBackground; } }
            public override Color CheckPressedBackground { get { return _theme.SelectionBackground; } }
            public override Color SeparatorDark { get { return _theme.Border; } }
            public override Color SeparatorLight { get { return _theme.Background; } }
        }
    }
}
