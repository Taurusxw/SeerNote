using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using SeerNote.Domain;
using SeerNote.Platform;
using SeerNote.Storage;

namespace SeerNote.Presentation
{
    public sealed class MainViewModel : IDisposable
    {
        private readonly PortableStore _store;
        private readonly ClipboardService _clipboard;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _saveTimer;
        private readonly object _saveGate = new object();
        private Task<SaveResult> _activeSaveTask;
        private int _version;
        private int _savedVersion;
        private bool _disposed;
        private string _searchText;
        private string _selectedCategory;
        private Entry _selectedEntry;
        private SmartView _selectedView;
        private string _statusText;
        private bool _statusIsError;

        public MainViewModel(AppState state, PortableStore store, ClipboardService clipboard, Dispatcher dispatcher)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _searchText = String.Empty;
            _selectedView = State.Settings == null ? SmartView.All : State.Settings.LastSmartView;
            _statusText = "就绪";

            _saveTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _saveTimer.Tick += SaveTimerOnTick;

            _selectedEntry = GetFilteredEntries().FirstOrDefault();
        }

        public event EventHandler ContentChanged;
        public event EventHandler StatusChanged;

        public AppState State { get; private set; }

        public Entry SelectedEntry
        {
            get { return _selectedEntry; }
        }

        public SmartView SelectedView
        {
            get { return _selectedView; }
        }

        public string SearchText
        {
            get { return _searchText; }
        }

        public string SelectedCategory
        {
            get { return _selectedCategory; }
        }

        public string StatusText
        {
            get { return _statusText; }
        }

        public bool StatusIsError
        {
            get { return _statusIsError; }
        }

        public CloseButtonBehavior CloseButtonBehavior
        {
            get { return State.Settings.CloseButtonBehavior; }
        }

        public AppTheme AppTheme
        {
            get { return State.Settings.Theme; }
        }

        public bool HasUnsavedChanges
        {
            get { return _version > _savedVersion; }
        }

        public int TrashCount
        {
            get { return State.Entries.Count(entry => entry != null && entry.IsDeleted); }
        }

        public IList<Entry> GetFilteredEntries()
        {
            IEnumerable<Entry> result = EntrySearch.Filter(State.Entries, _searchText, _selectedView);
            if (!String.IsNullOrWhiteSpace(_selectedCategory))
            {
                string category = _selectedCategory;
                result = result.Where(entry => String.Equals(entry.Category, category, StringComparison.InvariantCultureIgnoreCase));
            }
            return new ReadOnlyCollection<Entry>(result.ToList());
        }

        public IList<string> GetCategories()
        {
            return new ReadOnlyCollection<string>(new List<string>(State.Categories));
        }

        public void SetSearchText(string value)
        {
            value = value ?? String.Empty;
            if (String.Equals(_searchText, value, StringComparison.Ordinal))
            {
                return;
            }
            _searchText = value;
            EnsureVisibleSelection();
            RaiseContentChanged();
        }

        public void SelectView(SmartView view)
        {
            if (!Enum.IsDefined(typeof(SmartView), view))
            {
                throw new ArgumentOutOfRangeException(nameof(view));
            }
            if (_selectedView == view && _selectedCategory == null)
            {
                return;
            }

            _selectedView = view;
            _selectedCategory = null;
            State.Settings.LastSmartView = view;
            MarkChanged();
            EnsureVisibleSelection();
            RaiseContentChanged();
        }

        public void SelectCategory(string category)
        {
            string normalized = String.IsNullOrWhiteSpace(category) ? null : category.Trim();
            if (String.Equals(_selectedCategory, normalized, StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }
            _selectedCategory = normalized;
            if (normalized != null && _selectedView != SmartView.All)
            {
                _selectedView = SmartView.All;
                State.Settings.LastSmartView = _selectedView;
                MarkChanged();
            }
            EnsureVisibleSelection();
            RaiseContentChanged();
        }

        public void SelectEntry(Entry entry)
        {
            if (ReferenceEquals(_selectedEntry, entry))
            {
                return;
            }
            _selectedEntry = entry;
            RaiseContentChanged();
        }

        public Entry CreateEntry()
        {
            ThrowIfDisposed();
            var now = DateTime.UtcNow;
            var entry = new Entry
            {
                Title = (_searchText ?? String.Empty).Trim(),
                Category = _selectedCategory ?? String.Empty,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            State.Entries.Add(entry);
            _selectedEntry = entry;
            _selectedView = SmartView.All;
            State.Settings.LastSmartView = _selectedView;
            _searchText = String.Empty;
            MarkChanged();
            RaiseContentChanged();
            return entry;
        }

        public void UpdateSelectedTitle(string title)
        {
            UpdateSelectedText(delegate(Entry entry) { entry.Title = title ?? String.Empty; }, _selectedEntry == null ? null : _selectedEntry.Title, title);
        }

        public void UpdateSelectedBody(string body)
        {
            UpdateSelectedText(delegate(Entry entry) { entry.Body = body ?? String.Empty; }, _selectedEntry == null ? null : _selectedEntry.Body, body);
        }

        public bool CreateCategory(string category, out string error)
        {
            string normalized = NormalizeCategory(category);
            if (normalized == null)
            {
                error = "分类名称不能为空。";
                return false;
            }
            if (FindCategoryIndex(normalized) >= 0)
            {
                error = "已经存在同名分类。";
                return false;
            }
            State.Categories.Add(normalized);
            _selectedCategory = normalized;
            _selectedView = SmartView.All;
            State.Settings.LastSmartView = _selectedView;
            MarkChanged();
            EnsureVisibleSelection();
            RaiseContentChanged();
            error = null;
            return true;
        }

        public bool RenameCategory(string category, string newName, out string error)
        {
            int index = FindCategoryIndex(category);
            string normalized = NormalizeCategory(newName);
            if (index < 0)
            {
                error = "找不到要重命名的分类。";
                return false;
            }
            if (normalized == null)
            {
                error = "分类名称不能为空。";
                return false;
            }
            int duplicate = FindCategoryIndex(normalized);
            if (duplicate >= 0 && duplicate != index)
            {
                error = "已经存在同名分类。";
                return false;
            }
            string prior = State.Categories[index];
            if (String.Equals(prior, normalized, StringComparison.Ordinal))
            {
                error = null;
                return true;
            }
            State.Categories[index] = normalized;
            foreach (Entry entry in State.Entries)
            {
                if (entry != null && String.Equals(entry.Category, prior, StringComparison.InvariantCultureIgnoreCase))
                {
                    entry.Category = normalized;
                }
            }
            if (String.Equals(_selectedCategory, prior, StringComparison.InvariantCultureIgnoreCase))
            {
                _selectedCategory = normalized;
            }
            MarkChanged();
            RaiseContentChanged();
            error = null;
            return true;
        }

        public bool DeleteCategory(string category)
        {
            int index = FindCategoryIndex(category);
            if (index < 0)
            {
                return false;
            }
            string removed = State.Categories[index];
            State.Categories.RemoveAt(index);
            foreach (Entry entry in State.Entries)
            {
                if (entry != null && String.Equals(entry.Category, removed, StringComparison.InvariantCultureIgnoreCase))
                {
                    entry.Category = String.Empty;
                }
            }
            if (String.Equals(_selectedCategory, removed, StringComparison.InvariantCultureIgnoreCase))
            {
                _selectedCategory = null;
            }
            MarkChanged();
            EnsureVisibleSelection();
            RaiseContentChanged();
            return true;
        }

        public bool ReorderCategory(string category, string targetCategory, bool insertAfter)
        {
            int sourceIndex = FindCategoryIndex(category);
            int targetIndex = FindCategoryIndex(targetCategory);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            {
                return false;
            }
            string moved = State.Categories[sourceIndex];
            State.Categories.RemoveAt(sourceIndex);
            targetIndex = FindCategoryIndex(targetCategory);
            int insertIndex = insertAfter ? targetIndex + 1 : targetIndex;
            State.Categories.Insert(insertIndex, moved);
            MarkChanged();
            RaiseContentChanged();
            return true;
        }

        public bool MoveEntryToCategory(Guid entryId, string category)
        {
            Entry entry = State.Entries.FirstOrDefault(candidate => candidate != null && candidate.Id == entryId);
            string normalized = NormalizeCategory(category);
            if (entry == null || entry.IsDeleted || (normalized != null && FindCategoryIndex(normalized) < 0))
            {
                return false;
            }
            string destination = normalized ?? String.Empty;
            if (String.Equals(entry.Category, destination, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            entry.Category = destination;
            entry.UpdatedUtc = DateTime.UtcNow;
            MarkChanged();
            EnsureVisibleSelection();
            RaiseContentChanged();
            return true;
        }

        public void ToggleFavorite()
        {
            if (_selectedEntry == null || _selectedEntry.IsDeleted)
            {
                return;
            }
            _selectedEntry.IsFavorite = !_selectedEntry.IsFavorite;
            TouchSelected();
        }

        public Entry SoftDeleteSelected()
        {
            if (_selectedEntry == null || _selectedEntry.IsDeleted)
            {
                return null;
            }
            Entry deleted = _selectedEntry;
            deleted.IsDeleted = true;
            deleted.DeletedUtc = DateTime.UtcNow;
            deleted.UpdatedUtc = deleted.DeletedUtc.Value;
            if (deleted.Sticky != null)
            {
                deleted.Sticky.IsOpen = false;
            }
            MarkChanged();
            EnsureVisibleSelection();
            RaiseContentChanged();
            return deleted;
        }

        public Entry RestoreSelected()
        {
            if (_selectedEntry == null || !_selectedEntry.IsDeleted)
            {
                return null;
            }
            Entry restored = _selectedEntry;
            restored.IsDeleted = false;
            restored.DeletedUtc = null;
            restored.UpdatedUtc = DateTime.UtcNow;
            MarkChanged();
            EnsureVisibleSelection();
            RaiseContentChanged();
            return restored;
        }

        public Entry PermanentlyDeleteSelected()
        {
            if (_selectedEntry == null || !_selectedEntry.IsDeleted)
            {
                return null;
            }
            Entry removed = _selectedEntry;
            State.Entries.Remove(removed);
            _selectedEntry = null;
            MarkChanged();
            EnsureVisibleSelection();
            RaiseContentChanged();
            return removed;
        }

        public int ClearTrash()
        {
            ThrowIfDisposed();
            List<Entry> removed = State.Entries.Where(entry => entry != null && entry.IsDeleted).ToList();
            if (removed.Count == 0)
            {
                return 0;
            }

            foreach (Entry entry in removed)
            {
                State.Entries.Remove(entry);
            }
            EnsureVisibleSelection();
            MarkChanged();
            RaiseContentChanged();
            return removed.Count;
        }

        public void NotifyExternalEntryChanged(Entry entry)
        {
            if (_disposed || entry == null || !State.Entries.Contains(entry))
            {
                return;
            }
            MarkChanged();
            RaiseContentChanged();
        }

        public ClipboardResult CopyText(string text)
        {
            ClipboardResult result = _clipboard.TrySetText(text ?? String.Empty);
            if (result.Succeeded)
            {
                string title = _selectedEntry == null ? "内容" : DisplayTitle(_selectedEntry);
                ReportStatus("已复制：《" + title + "》", false);
            }
            else
            {
                ReportStatus("复制失败：剪贴板暂时不可用，请重试。", true);
            }
            return result;
        }

        public SaveResult Export(string path)
        {
            SaveResult result = _store.Export(path, State.Clone());
            ReportStatus(result.Success ? "已导出备份：" + result.Path : "导出失败：" + result.Error.Message, !result.Success);
            return result;
        }

        public void UpdateWindowBounds(double left, double top, double width, double height)
        {
            WindowBounds bounds = State.Settings.WindowBounds;
            if (bounds == null)
            {
                bounds = new WindowBounds();
                State.Settings.WindowBounds = bounds;
            }
            if (NearlyEqual(bounds.Left, left) && NearlyEqual(bounds.Top, top) && NearlyEqual(bounds.Width, width) && NearlyEqual(bounds.Height, height))
            {
                return;
            }
            bounds.Left = left;
            bounds.Top = top;
            bounds.Width = width;
            bounds.Height = height;
            MarkChanged();
        }

        public void UpdateCloseButtonBehavior(CloseButtonBehavior behavior)
        {
            if (!Enum.IsDefined(typeof(CloseButtonBehavior), behavior))
            {
                throw new ArgumentOutOfRangeException(nameof(behavior));
            }
            if (State.Settings.CloseButtonBehavior == behavior)
            {
                return;
            }

            State.Settings.CloseButtonBehavior = behavior;
            MarkChanged();
        }

        public void UpdateAppTheme(AppTheme theme)
        {
            if (!Enum.IsDefined(typeof(AppTheme), theme))
            {
                throw new ArgumentOutOfRangeException(nameof(theme));
            }
            if (State.Settings.Theme == theme)
            {
                return;
            }

            State.Settings.Theme = theme;
            MarkChanged();
        }

        public void RequestImmediateSave()
        {
            ThrowIfDisposed();
            _saveTimer.Stop();
            StartBackgroundSave();
        }

        public bool Flush()
        {
            ThrowIfDisposed();
            _saveTimer.Stop();

            Task<SaveResult> prior;
            lock (_saveGate)
            {
                prior = _activeSaveTask;
            }
            if (prior != null && !prior.IsCompleted)
            {
                try
                {
                    prior.Wait();
                }
                catch (AggregateException)
                {
                    // The foreground save below is authoritative and reports its own failure.
                }
            }

            lock (_saveGate)
            {
                _activeSaveTask = null;
            }

            if (!HasUnsavedChanges && !_statusIsError)
            {
                return true;
            }

            int version = _version;
            SaveResult result = _store.Save(State.Clone());
            ApplySaveResult(version, result);
            return result.Success;
        }

        public void ReportStatus(string text, bool isError)
        {
            _statusText = String.IsNullOrWhiteSpace(text) ? "就绪" : text;
            _statusIsError = isError;
            RaiseStatusChanged();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _saveTimer.Stop();
            _saveTimer.Tick -= SaveTimerOnTick;
        }

        private void UpdateSelectedText(Action<Entry> update, string oldValue, string newValue)
        {
            if (_selectedEntry == null || _selectedEntry.IsDeleted || String.Equals(oldValue ?? String.Empty, newValue ?? String.Empty, StringComparison.Ordinal))
            {
                return;
            }
            update(_selectedEntry);
            TouchSelected();
        }

        private void TouchSelected()
        {
            _selectedEntry.UpdatedUtc = DateTime.UtcNow;
            MarkChanged();
            RaiseContentChanged();
        }

        private void EnsureVisibleSelection()
        {
            IList<Entry> visible = GetFilteredEntries();
            if (_selectedEntry == null || !visible.Contains(_selectedEntry))
            {
                _selectedEntry = visible.FirstOrDefault();
            }
        }

        private void MarkChanged()
        {
            if (_disposed)
            {
                return;
            }
            _version++;
            _statusText = "尚未保存";
            _statusIsError = false;
            _saveTimer.Stop();
            _saveTimer.Start();
            RaiseStatusChanged();
        }

        private void SaveTimerOnTick(object sender, EventArgs eventArgs)
        {
            _saveTimer.Stop();
            StartBackgroundSave();
        }

        private void StartBackgroundSave()
        {
            if (_disposed || !HasUnsavedChanges)
            {
                return;
            }

            lock (_saveGate)
            {
                if (_activeSaveTask != null && !_activeSaveTask.IsCompleted)
                {
                    _saveTimer.Stop();
                    _saveTimer.Start();
                    return;
                }

                int version = _version;
                AppState snapshot = State.Clone();
                _statusText = "保存中…";
                _statusIsError = false;
                RaiseStatusChanged();

                Task<SaveResult> task = Task.Run(delegate { return _store.Save(snapshot); });
                _activeSaveTask = task;
                task.ContinueWith(delegate(Task<SaveResult> completed)
                {
                    SaveResult result;
                    if (completed.Status == TaskStatus.RanToCompletion)
                    {
                        result = completed.Result;
                    }
                    else
                    {
                        Exception error = completed.Exception == null ? new InvalidOperationException("后台保存失败。") : completed.Exception.GetBaseException();
                        result = new SaveResult(
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "notes.json"),
                            DateTime.MinValue,
                            error);
                    }
                    _dispatcher.BeginInvoke(new Action(delegate { CompleteBackgroundSave(task, version, result); }));
                }, TaskScheduler.Default);
            }
        }

        private void CompleteBackgroundSave(Task<SaveResult> task, int version, SaveResult result)
        {
            if (_disposed)
            {
                return;
            }
            lock (_saveGate)
            {
                if (!ReferenceEquals(_activeSaveTask, task))
                {
                    return;
                }
                _activeSaveTask = null;
            }

            ApplySaveResult(version, result);
            if (result.Success && HasUnsavedChanges)
            {
                StartBackgroundSave();
            }
        }

        private void ApplySaveResult(int version, SaveResult result)
        {
            if (result.Success)
            {
                _savedVersion = Math.Max(_savedVersion, version);
                State.SavedUtc = result.SavedUtc;
                if (HasUnsavedChanges)
                {
                    _statusText = "保存中…";
                }
                else
                {
                    _statusText = "已保存 · " + result.SavedUtc.ToLocalTime().ToString("HH:mm:ss");
                }
                _statusIsError = false;
            }
            else
            {
                _statusText = "保存失败：" + (result.Error == null ? "未知错误" : result.Error.Message);
                _statusIsError = true;
            }
            RaiseStatusChanged();
        }

        private void RaiseContentChanged()
        {
            EventHandler handler = ContentChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseStatusChanged()
        {
            EventHandler handler = StatusChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MainViewModel));
            }
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.5;
        }

        private static string DisplayTitle(Entry entry)
        {
            string title = entry == null ? String.Empty : entry.DisplayTitle;
            return String.IsNullOrWhiteSpace(title) ? "未命名" : title;
        }

        private int FindCategoryIndex(string category)
        {
            if (String.IsNullOrWhiteSpace(category))
            {
                return -1;
            }
            for (int index = 0; index < State.Categories.Count; index++)
            {
                if (String.Equals(State.Categories[index], category.Trim(), StringComparison.InvariantCultureIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        private static string NormalizeCategory(string category)
        {
            return String.IsNullOrWhiteSpace(category) ? null : category.Trim();
        }

    }
}
