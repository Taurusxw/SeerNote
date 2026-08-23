using System;
using System.Windows;

namespace SeerNote.Presentation
{
    public enum MainWindowLayoutClass
    {
        Compact,
        Standard,
        Wide,
        UltraWide
    }

    public sealed class MainWindowLayout
    {
        public MainWindowLayout(MainWindowLayoutClass layoutClass, double sidebarWidth, double listWidth, double editorMinimumWidth)
        {
            LayoutClass = layoutClass;
            SidebarWidth = sidebarWidth;
            ListWidth = listWidth;
            EditorMinimumWidth = editorMinimumWidth;
        }

        public MainWindowLayoutClass LayoutClass { get; private set; }

        public double SidebarWidth { get; private set; }

        public double ListWidth { get; private set; }

        public double EditorMinimumWidth { get; private set; }
    }

    public static class MainWindowLayoutCalculator
    {
        public const double MinimumWindowWidth = 860.0;
        public const double MinimumWindowHeight = 540.0;
        public const double BaselineWindowWidth = 1080.0;
        public const double BaselineWindowHeight = 720.0;
        public const double MaximumStartupWidth = 1920.0;
        public const double MaximumStartupHeight = 1280.0;

        private const double StartupWidthRatio = 0.68;
        private const double StartupHeightRatio = 0.75;
        private const double LayoutGrid = 8.0;

        public static Size GetStartupSize(Size workAreaSize)
        {
            double workWidth = IsFinitePositive(workAreaSize.Width) ? workAreaSize.Width : BaselineWindowWidth;
            double workHeight = IsFinitePositive(workAreaSize.Height) ? workAreaSize.Height : BaselineWindowHeight;
            double preferredWidth = Clamp(SnapDown(workWidth * StartupWidthRatio), BaselineWindowWidth, MaximumStartupWidth);
            double preferredHeight = Clamp(SnapDown(workHeight * StartupHeightRatio), BaselineWindowHeight, MaximumStartupHeight);

            return new Size(
                Math.Max(Math.Min(MinimumWindowWidth, workWidth), Math.Min(preferredWidth, workWidth)),
                Math.Max(Math.Min(MinimumWindowHeight, workHeight), Math.Min(preferredHeight, workHeight)));
        }

        public static MainWindowLayout GetLayout(double clientWidth)
        {
            double width = IsFinitePositive(clientWidth) ? clientWidth : BaselineWindowWidth;
            if (width < 1100.0)
            {
                return new MainWindowLayout(MainWindowLayoutClass.Compact, 176.0, 304.0, 360.0);
            }
            if (width < 1550.0)
            {
                return new MainWindowLayout(MainWindowLayoutClass.Standard, 200.0, 360.0, 420.0);
            }
            if (width < 1900.0)
            {
                return new MainWindowLayout(MainWindowLayoutClass.Wide, 228.0, 420.0, 560.0);
            }
            return new MainWindowLayout(MainWindowLayoutClass.UltraWide, 252.0, 480.0, 680.0);
        }

        private static double SnapDown(double value)
        {
            return Math.Floor(value / LayoutGrid) * LayoutGrid;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(value, maximum));
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0 && !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }
}
