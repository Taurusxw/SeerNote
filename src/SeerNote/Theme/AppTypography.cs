using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace SeerNote.Theme
{
    /// <summary>
    /// Resolves the application-private mixed Chinese/Latin font from the
    /// portable distribution without installing or registering it in Windows.
    /// </summary>
    public static class AppTypography
    {
        public const string BundledFontFileName = "SourceHanSansCN-Regular.otf";
        public const string BundledFontDirectoryName = "fonts";

        private static readonly Lazy<FontState> CurrentState = new Lazy<FontState>(LoadCurrent, true);

        public static FontFamily CurrentFontFamily
        {
            get { return CurrentState.Value.FontFamily; }
        }

        public static bool IsBundledFontAvailable
        {
            get { return CurrentState.Value.IsBundled; }
        }

        public static Exception LoadError
        {
            get { return CurrentState.Value.Error; }
        }

        public static string BundledFontPath
        {
            get { return CurrentState.Value.FontPath; }
        }

        private static FontState LoadCurrent()
        {
            string assemblyPath = typeof(AppTypography).Assembly.Location;
            string assemblyDirectory = String.IsNullOrWhiteSpace(assemblyPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(assemblyPath);
            string fontDirectory = Path.Combine(assemblyDirectory, BundledFontDirectoryName);
            string fontPath = Path.Combine(fontDirectory, BundledFontFileName);
            if (!File.Exists(fontPath))
            {
                return FontState.Fallback(fontPath, new FileNotFoundException("缺少 SeerNote 私有字体。", fontPath));
            }

            try
            {
                var fontDirectoryUri = new Uri(Path.GetFullPath(fontDirectory) + Path.DirectorySeparatorChar, UriKind.Absolute);
                FontFamily family = Fonts.GetFontFamilies(fontDirectoryUri)
                    .FirstOrDefault(candidate => candidate.Source.IndexOf("Source Han Sans CN", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? Fonts.GetFontFamilies(fontDirectoryUri).FirstOrDefault();
                return family == null
                    ? FontState.Fallback(fontPath, new InvalidDataException("无法从私有字体目录解析字体家族。"))
                    : new FontState(family, true, fontPath, null);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return FontState.Fallback(fontPath, error);
            }
        }

        private sealed class FontState
        {
            public FontState(FontFamily fontFamily, bool isBundled, string fontPath, Exception error)
            {
                FontFamily = fontFamily;
                IsBundled = isBundled;
                FontPath = fontPath;
                Error = error;
            }

            public FontFamily FontFamily { get; private set; }

            public bool IsBundled { get; private set; }

            public string FontPath { get; private set; }

            public Exception Error { get; private set; }

            public static FontState Fallback(string fontPath, Exception error)
            {
                return new FontState(new FontFamily("Segoe UI"), false, fontPath, error);
            }
        }
    }
}
