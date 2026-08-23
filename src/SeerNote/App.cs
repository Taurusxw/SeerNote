using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SeerNote.Domain;
using SeerNote.Platform;
using SeerNote.Presentation;
using SeerNote.Storage;
using SeerNote.Theme;

namespace SeerNote
{
    public sealed class App : Application
    {
        private readonly string _applicationRoot;
        private SingleInstanceGuard _instanceGuard;
        private GlobalHotkeyService _hotkey;
        private TrayIconService _tray;
        private MainViewModel _viewModel;
        private MainWindow _window;
        private bool _isExiting;
        private bool _exitRequestPending;

        private App(string applicationRoot, SingleInstanceGuard instanceGuard)
        {
            _applicationRoot = applicationRoot;
            _instanceGuard = instanceGuard;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Resources.MergedDictionaries.Add(ThemeResources.Create());
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            SessionEnding += OnSessionEnding;
        }

        [STAThread]
        public static int Main()
        {
            string root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            string dataDirectory = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDirectory);
            SingleInstanceGuard instanceGuard;
            string applicationId = "SeerNote|" + SingleInstanceGuard.GetDirectoryIdentity(root);
            string lockFilePath = Path.Combine(dataDirectory, ".seernote.lock");
            if (!SingleInstanceGuard.TryAcquire(applicationId, lockFilePath, out instanceGuard))
            {
                return 0;
            }

            var app = new App(root, instanceGuard);
            return app.RunSeerNote();
        }

        private int RunSeerNote()
        {
            try
            {
                var store = new PortableStore(_applicationRoot);
                LoadResult load = store.Load();
                if (load.State == null)
                {
                    MessageBox.Show(
                        "SeerNote 无法读取数据，也没有找到可恢复的备份。\n\n" + ErrorMessage(load.Error),
                        "无法启动 SeerNote",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    DisposeServices();
                    return 2;
                }

                ThemeResources.ApplyTheme(Resources, load.State.Settings.Theme);

                var clipboard = new ClipboardService();
                _viewModel = new MainViewModel(load.State, store, clipboard, Dispatcher);
                _window = new MainWindow(_viewModel);
                MainWindow = _window;
                ApplyExecutableIcon();
                WireApplicationEvents();
                CreateTrayIcon();
                _tray.Show();
                _window.Show();

                if (load.Recovery != null && load.Recovery.Recovered)
                {
                    _window.ReportStatus("主数据不可读，已从有效备份恢复；原文件已保留。", false);
                }
                else if (load.Error != null)
                {
                    _window.ReportStatus("已读取恢复副本，但主文件恢复失败：" + ErrorMessage(load.Error), true);
                }
                else if (!AppTypography.IsBundledFontAvailable)
                {
                    _window.ReportStatus("私有字体不可用，已回退 Windows 系统字体：" + ErrorMessage(AppTypography.LoadError), true);
                }

                Run();
                DisposeServices();
                return 0;
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    "SeerNote 启动失败。\n\n" + ErrorMessage(error),
                    "SeerNote",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                DisposeServices();
                return 1;
            }
        }

        private void WireApplicationEvents()
        {
            _instanceGuard.ActivationRequested += InstanceGuardOnActivationRequested;
            _window.SourceInitialized += WindowOnSourceInitialized;
            _window.Closing += WindowOnClosing;
            _window.ExitRequested += ExitRequested;
            _window.ThemeChanged += WindowOnThemeChanged;
        }

        private void CreateTrayIcon()
        {
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            Icon extracted = Icon.ExtractAssociatedIcon(executable);
            using (Icon icon = extracted ?? (Icon)SystemIcons.Application.Clone())
            {
                _tray = new TrayIconService(icon, "SeerNote · 本地 Note", new TrayMenuLabels("显示 SeerNote", "新建条目", "退出"));
            }
            _tray.ApplyTheme(CreateTrayMenuTheme());
            _tray.ShowRequested += delegate { Dispatcher.BeginInvoke(new Action(delegate { _window.ShowAndFocus(true); })); };
            _tray.NewRequested += delegate { Dispatcher.BeginInvoke(new Action(delegate { _window.CreateAndEdit(); })); };
            _tray.ExitRequested += delegate { Dispatcher.BeginInvoke(new Action(RequestExit)); };
        }

        private void ApplyExecutableIcon()
        {
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            using (Icon icon = Icon.ExtractAssociatedIcon(executable))
            {
                if (icon == null)
                {
                    return;
                }
                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                _window.Icon = source;
            }
        }

        private void WindowOnSourceInitialized(object sender, EventArgs eventArgs)
        {
            _hotkey = new GlobalHotkeyService();
            _hotkey.HotkeyPressed += delegate { _window.ShowAndFocus(true); };
            IntPtr handle = new WindowInteropHelper(_window).Handle;
            HotkeyRegistrationResult result = _hotkey.TryRegister(handle, ModifierKeys.Control | ModifierKeys.Shift, Key.Space);
            if (!result.Succeeded)
            {
                _window.ReportStatus("全局快捷键已被其他程序占用；仍可从托盘打开 SeerNote。", true);
            }
        }

        private void InstanceGuardOnActivationRequested(object sender, EventArgs eventArgs)
        {
            if (_window == null || _window.Dispatcher.HasShutdownStarted)
            {
                return;
            }
            _window.Dispatcher.BeginInvoke(new Action(delegate { _window.ShowAndFocus(true); }));
        }

        private void WindowOnThemeChanged(object sender, EventArgs eventArgs)
        {
            if (_tray != null)
            {
                _tray.ApplyTheme(CreateTrayMenuTheme());
            }
        }

        private void WindowOnClosing(object sender, CancelEventArgs eventArgs)
        {
            if (_isExiting)
            {
                return;
            }

            eventArgs.Cancel = true;
            if (_viewModel.CloseButtonBehavior == CloseButtonBehavior.MinimizeToTray)
            {
                _window.SaveCurrentBounds();
                bool saved = _viewModel.Flush();
                if (!saved)
                {
                    _window.ReportStatus("尚未写入磁盘。内容仍保留在当前进程；请重试或导出。", true);
                }
                _window.Hide();
                if (saved)
                {
                    _window.ReportStatus("SeerNote 已最小化到系统托盘。", false);
                }
                return;
            }

            if (_exitRequestPending)
            {
                return;
            }

            _exitRequestPending = true;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                _exitRequestPending = false;
                RequestExit();
            }));
        }

        private void ExitRequested(object sender, EventArgs eventArgs)
        {
            RequestExit();
        }

        private void RequestExit()
        {
            if (_isExiting || _window == null)
            {
                return;
            }

            _window.SaveCurrentBounds();
            while (!_viewModel.Flush())
            {
                _window.ShowAndFocus(false);
                MessageBoxResult choice = MessageBox.Show(
                    _window,
                    "最新修改尚未写入磁盘。\n\n选择“是”重试保存；选择“否”将放弃尚未保存的修改并退出；选择“取消”返回 SeerNote。",
                    "保存失败",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel);
                if (choice == MessageBoxResult.Cancel)
                {
                    return;
                }
                if (choice == MessageBoxResult.No)
                {
                    break;
                }
            }

            _isExiting = true;
            DisposeServices();
            if (_window != null)
            {
                _window.Close();
            }
            Shutdown(0);
        }

        private void OnSessionEnding(object sender, SessionEndingCancelEventArgs eventArgs)
        {
            if (_viewModel != null)
            {
                _window.SaveCurrentBounds();
                _viewModel.Flush();
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs eventArgs)
        {
            MessageBox.Show(
                _window,
                "SeerNote 遇到未处理错误。当前内存内容会保留到应用关闭，请先尝试导出。\n\n" + ErrorMessage(eventArgs.Exception),
                "SeerNote 错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            eventArgs.Handled = true;
        }

        private void DisposeServices()
        {
            if (_window != null)
            {
                _window.SourceInitialized -= WindowOnSourceInitialized;
                _window.Closing -= WindowOnClosing;
                _window.ExitRequested -= ExitRequested;
                _window.ThemeChanged -= WindowOnThemeChanged;
            }
            if (_instanceGuard != null)
            {
                _instanceGuard.ActivationRequested -= InstanceGuardOnActivationRequested;
            }
            if (_hotkey != null)
            {
                _hotkey.Dispose();
                _hotkey = null;
            }
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
            if (_window != null)
            {
                _window.Dispose();
            }
            if (_instanceGuard != null)
            {
                _instanceGuard.Dispose();
                _instanceGuard = null;
            }
        }

        private static string ErrorMessage(Exception error)
        {
            return error == null ? "未知错误" : error.GetBaseException().Message;
        }

        private TrayMenuTheme CreateTrayMenuTheme()
        {
            if (SystemParameters.HighContrast)
            {
                return TrayMenuTheme.SystemDefault;
            }

            return new TrayMenuTheme(
                DrawingColor(ThemeResources.SurfaceRaisedBrushKey),
                DrawingColor(ThemeResources.InkBrushKey),
                DrawingColor(ThemeResources.BorderBrushKey),
                DrawingColor(ThemeResources.SurfaceBrushKey),
                DrawingColor(ThemeResources.AccentHoverBrushKey));
        }

        private Color DrawingColor(string resourceKey)
        {
            var brush = Resources[resourceKey] as System.Windows.Media.SolidColorBrush;
            if (brush == null)
            {
                throw new InvalidOperationException("Theme resource is not a solid brush: " + resourceKey);
            }
            System.Windows.Media.Color color = brush.Color;
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}
