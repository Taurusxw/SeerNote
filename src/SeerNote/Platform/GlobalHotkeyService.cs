using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;

namespace SeerNote.Platform
{
    public enum HotkeyRegistrationFailure
    {
        None,
        InvalidWindowHandle,
        UnsupportedKey,
        RegistrationFailed
    }

    public sealed class HotkeyRegistrationResult
    {
        private HotkeyRegistrationResult(bool succeeded, HotkeyRegistrationFailure failure, int errorCode)
        {
            Succeeded = succeeded;
            Failure = failure;
            ErrorCode = errorCode;
        }

        public bool Succeeded { get; private set; }

        public HotkeyRegistrationFailure Failure { get; private set; }

        public int ErrorCode { get; private set; }

        internal static HotkeyRegistrationResult Success()
        {
            return new HotkeyRegistrationResult(true, HotkeyRegistrationFailure.None, 0);
        }

        internal static HotkeyRegistrationResult Failed(HotkeyRegistrationFailure failure, int errorCode)
        {
            return new HotkeyRegistrationResult(false, failure, errorCode);
        }
    }

    /// <summary>
    /// Registers one process hotkey and translates WM_HOTKEY into a managed event.
    /// </summary>
    public sealed class GlobalHotkeyService : IDisposable
    {
        private static int _nextId;
        private readonly int _id = Interlocked.Increment(ref _nextId);
        private HwndSource _source;
        private IntPtr _windowHandle;
        private bool _isRegistered;
        private bool _disposed;

        public event EventHandler HotkeyPressed;

        public bool IsRegistered
        {
            get { return _isRegistered; }
        }

        public HotkeyRegistrationResult TryRegister(IntPtr windowHandle, ModifierKeys modifiers, Key key)
        {
            ThrowIfDisposed();
            Unregister();

            if (windowHandle == IntPtr.Zero)
            {
                return HotkeyRegistrationResult.Failed(HotkeyRegistrationFailure.InvalidWindowHandle, 0);
            }

            int virtualKey;
            try
            {
                virtualKey = KeyInterop.VirtualKeyFromKey(key);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return HotkeyRegistrationResult.Failed(HotkeyRegistrationFailure.UnsupportedKey, 0);
            }

            if (virtualKey == 0)
            {
                return HotkeyRegistrationResult.Failed(HotkeyRegistrationFailure.UnsupportedKey, 0);
            }

            HwndSource source = HwndSource.FromHwnd(windowHandle);
            if (source == null)
            {
                return HotkeyRegistrationResult.Failed(HotkeyRegistrationFailure.InvalidWindowHandle, 0);
            }

            uint nativeModifiers = (uint)(modifiers & (ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Windows));
            if (!NativeMethods.RegisterHotKey(windowHandle, _id, nativeModifiers, (uint)virtualKey))
            {
                return HotkeyRegistrationResult.Failed(HotkeyRegistrationFailure.RegistrationFailed, Marshal.GetLastWin32Error());
            }

            _source = source;
            _windowHandle = windowHandle;
            _source.AddHook(WindowMessageHook);
            _isRegistered = true;
            return HotkeyRegistrationResult.Success();
        }

        public void Unregister()
        {
            if (_source != null)
            {
                _source.RemoveHook(WindowMessageHook);
                _source = null;
            }

            if (_isRegistered)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, _id);
                _isRegistered = false;
                _windowHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Unregister();
            _disposed = true;
            HotkeyPressed = null;
        }

        private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WmHotkey && wParam.ToInt32() == _id)
            {
                handled = true;
                EventHandler handler = HotkeyPressed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }

            return IntPtr.Zero;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GlobalHotkeyService));
            }
        }
    }
}
