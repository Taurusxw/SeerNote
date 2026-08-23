using System;
using System.Collections.Generic;
using SeerNote.Domain;

namespace SeerNote.Presentation
{
    /// <summary>
    /// Owns the one-window-per-entry invariant for sticky windows on the UI thread.
    /// </summary>
    public sealed class StickyWindowManager : IDisposable
    {
        private readonly Dictionary<Guid, StickyWindow> _windows;
        private readonly Action<Entry> _entryChanged;
        private bool _isDisposed;

        public StickyWindowManager(Action<Entry> entryChanged)
        {
            _entryChanged = entryChanged ?? throw new ArgumentNullException(nameof(entryChanged));
            _windows = new Dictionary<Guid, StickyWindow>();
        }

        public int OpenWindowCount
        {
            get { return _windows.Count; }
        }

        public StickyWindow OpenOrActivate(Entry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }
            ThrowIfDisposed();

            StickyWindow existing;
            if (_windows.TryGetValue(entry.Id, out existing))
            {
                if (existing.WindowState == System.Windows.WindowState.Minimized)
                {
                    existing.WindowState = System.Windows.WindowState.Normal;
                }
                existing.Show();
                existing.Activate();
                return existing;
            }

            var window = new StickyWindow(entry);
            window.EntryChanged += OnEntryChanged;
            window.WindowClosed += OnWindowClosed;
            _windows.Add(entry.Id, window);
            _entryChanged(entry);
            window.Show();
            window.Activate();
            return window;
        }

        public bool TryGetWindow(Guid entryId, out StickyWindow window)
        {
            return _windows.TryGetValue(entryId, out window);
        }

        public bool Close(Guid entryId)
        {
            StickyWindow window;
            if (!_windows.TryGetValue(entryId, out window))
            {
                return false;
            }

            window.Close();
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            var windows = new List<StickyWindow>(_windows.Values);
            foreach (var window in windows)
            {
                window.Close();
            }
            _windows.Clear();
        }

        private void OnEntryChanged(Entry entry)
        {
            _entryChanged(entry);
        }

        private void OnWindowClosed(StickyWindow window)
        {
            window.EntryChanged -= OnEntryChanged;
            window.WindowClosed -= OnWindowClosed;
            _windows.Remove(window.Entry.Id);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(StickyWindowManager));
            }
        }
    }
}
