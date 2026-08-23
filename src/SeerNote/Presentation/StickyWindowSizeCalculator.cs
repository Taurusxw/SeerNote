using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    /// <summary>
    /// Measures sticky-note text in device-independent pixels and chooses the
    /// smallest balanced window that can show it within explicit safety limits.
    /// </summary>
    public static class StickyWindowSizeCalculator
    {
        public const double MinimumWidth = 320.0;
        public const double MinimumHeight = 180.0;
        public const double MaximumWidth = 720.0;
        public const double MaximumHeight = 640.0;

        private const double WorkAreaReserve = 48.0;
        private const double HorizontalChrome = 56.0;
        private const double VerticalChrome = 72.0;
        private const double TitleChrome = 160.0;
        private const double CandidateStep = 32.0;
        private const double PreferredAspectRatio = 1.35;
        private const double AspectPenaltyWeight = 0.12;
        private const double BodyFontSize = 15.0;
        private const double TitleFontSize = 13.0;

        private static readonly Typeface InterfaceTypeface = new Typeface(
            AppTypography.CurrentFontFamily,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        public static Size Calculate(string title, string body, Size workAreaSize)
        {
            Size maximum = GetMaximumSize(workAreaSize);
            double titleWidth = MeasureNoWrap(String.IsNullOrWhiteSpace(title) ? "未命名便签" : title, TitleFontSize).Width + TitleChrome;
            double firstWidth = Clamp(Math.Ceiling(titleWidth), MinimumWidth, maximum.Width);

            FormattedText bodyMeasure = CreateFormattedText(String.IsNullOrEmpty(body) ? " " : body, BodyFontSize);

            Size bestFit = Size.Empty;
            double bestFitScore = Double.PositiveInfinity;
            Size bestOverflow = Size.Empty;
            double leastOverflow = Double.PositiveInfinity;

            double width = firstWidth;
            while (true)
            {
                double contentWidth = Math.Max(80.0, width - HorizontalChrome);
                bodyMeasure.MaxTextWidth = contentWidth;
                double desiredHeight = Math.Ceiling(bodyMeasure.Height + VerticalChrome);
                double height = Clamp(desiredHeight, MinimumHeight, maximum.Height);

                if (desiredHeight <= maximum.Height)
                {
                    double aspect = width / height;
                    double aspectPenalty = Math.Abs(Math.Log(aspect / PreferredAspectRatio));
                    double score = width * height * (1.0 + AspectPenaltyWeight * aspectPenalty);
                    if (score < bestFitScore)
                    {
                        bestFitScore = score;
                        bestFit = new Size(Math.Ceiling(width), Math.Ceiling(height));
                    }
                }
                else
                {
                    double overflow = desiredHeight - maximum.Height;
                    if (overflow < leastOverflow || (Math.Abs(overflow - leastOverflow) < 0.5 && (bestOverflow.IsEmpty || width < bestOverflow.Width)))
                    {
                        leastOverflow = overflow;
                        bestOverflow = new Size(Math.Ceiling(width), Math.Ceiling(maximum.Height));
                    }
                }

                if (width >= maximum.Width)
                {
                    break;
                }
                width = Math.Min(maximum.Width, width + CandidateStep);
            }

            return !bestFit.IsEmpty ? bestFit : bestOverflow;
        }

        public static Size GetMaximumSize(Size workAreaSize)
        {
            double availableWidth = IsFinitePositive(workAreaSize.Width) ? workAreaSize.Width - WorkAreaReserve : MaximumWidth;
            double availableHeight = IsFinitePositive(workAreaSize.Height) ? workAreaSize.Height - WorkAreaReserve : MaximumHeight;
            return new Size(
                Math.Max(MinimumWidth, Math.Min(MaximumWidth, availableWidth)),
                Math.Max(MinimumHeight, Math.Min(MaximumHeight, availableHeight)));
        }

        private static Size MeasureNoWrap(string text, double fontSize)
        {
            FormattedText measure = CreateFormattedText(text ?? String.Empty, fontSize);
            return new Size(measure.WidthIncludingTrailingWhitespace, measure.Height);
        }

        private static FormattedText CreateFormattedText(string text, double fontSize)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                InterfaceTypeface,
                fontSize,
                Brushes.Black,
                1.0);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(value, maximum));
        }

        private static bool IsFinitePositive(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value) && value > 0;
        }
    }
}
