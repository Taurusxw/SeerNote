using System;

namespace SeerNote.Domain
{
    public sealed class UserSettings
    {
        public UserSettings()
        {
            GlobalHotkey = "Ctrl+Shift+Space";
            WindowBounds = new WindowBounds();
            LastSmartView = SmartView.All;
            CloseButtonBehavior = CloseButtonBehavior.Exit;
            Theme = AppTheme.Graphite;
        }

        public string GlobalHotkey { get; set; }

        public WindowBounds WindowBounds { get; set; }

        public SmartView LastSmartView { get; set; }

        public CloseButtonBehavior CloseButtonBehavior { get; set; }

        public AppTheme Theme { get; set; }

        public UserSettings Clone()
        {
            return new UserSettings
            {
                GlobalHotkey = GlobalHotkey,
                WindowBounds = WindowBounds == null ? null : WindowBounds.Clone(),
                LastSmartView = LastSmartView,
                CloseButtonBehavior = CloseButtonBehavior,
                Theme = Theme
            };
        }

        public bool TryValidate(out string error)
        {
            if (String.IsNullOrWhiteSpace(GlobalHotkey))
            {
                error = "Global hotkey is required.";
                return false;
            }

            if (WindowBounds == null)
            {
                error = "Window bounds are required.";
                return false;
            }

            if (!WindowBounds.TryValidate(out error))
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(SmartView), LastSmartView))
            {
                error = "Last smart view is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(CloseButtonBehavior), CloseButtonBehavior))
            {
                error = "Close button behavior is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(AppTheme), Theme))
            {
                error = "Application theme is invalid.";
                return false;
            }

            error = null;
            return true;
        }

        public void Validate()
        {
            string error;
            if (!TryValidate(out error))
            {
                throw new InvalidOperationException(error);
            }
        }
    }
}
