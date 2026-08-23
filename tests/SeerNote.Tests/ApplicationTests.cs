using System;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using SeerNote.Domain;
using SeerNote.Platform;
using SeerNote.Presentation;
using SeerNote.Storage;

namespace SeerNote.Tests
{
    public static class ApplicationTests
    {
        private const string TemporaryDirectoryPrefix = "SeerNote.ApplicationTests-";

        public static void RunAll()
        {
            ViewModelDeletesRestoresAndPersists();
            ViewModelClearsTrashAndPersists();
            ViewModelManagesOrderedCategoriesAndUnifiedNotes();
            ViewModelUpdatesCloseButtonBehaviorAndPersists();
            SingleInstanceRejectsSecondOwner();
            DirectoryIdentityIgnoresCaseAndTrailingSeparators();
            LockFileRejectsDifferentNamedInstances();
        }

        private static void ViewModelUpdatesCloseButtonBehaviorAndPersists()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                using (var viewModel = new MainViewModel(new AppState(), store, new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    Require(viewModel.CloseButtonBehavior == CloseButtonBehavior.Exit, "View model should expose the safe default close behavior.");
                    Require(viewModel.AppTheme == AppTheme.Graphite, "View model should expose the compatible default theme.");
                    viewModel.UpdateCloseButtonBehavior(CloseButtonBehavior.MinimizeToTray);
                    viewModel.UpdateAppTheme(AppTheme.Porcelain);
                    Require(viewModel.CloseButtonBehavior == CloseButtonBehavior.MinimizeToTray, "View model should update close button behavior.");
                    Require(viewModel.AppTheme == AppTheme.Porcelain, "View model should update the application theme.");
                    Require(viewModel.Flush(), "Updated close button behavior should flush successfully.");
                }

                LoadResult loaded = store.Load();
                Require(loaded.Success, "Settings should reload after updating close button behavior.");
                Require(loaded.State.Settings.CloseButtonBehavior == CloseButtonBehavior.MinimizeToTray, "Updated close button behavior should persist.");
                Require(loaded.State.Settings.Theme == AppTheme.Porcelain, "Updated application theme should persist.");
            });
        }

        private static void ViewModelManagesOrderedCategoriesAndUnifiedNotes()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("工作");
                state.Categories.Add("生活");
                var store = new PortableStore(root);
                using (var viewModel = new MainViewModel(state, store, new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    string error;
                    Require(viewModel.CreateCategory("资料", out error), error);
                    Entry note = viewModel.CreateEntry();
                    Require(note.Category == "资料", "A new Note should inherit the selected custom category.");
                    Require(viewModel.RenameCategory("资料", "参考", out error), error);
                    Require(note.Category == "参考", "Renaming a category should update its Notes.");
                    Require(viewModel.ReorderCategory("参考", "工作", false), "Categories should support explicit drag order.");
                    Require(viewModel.GetCategories()[0] == "参考", "Reordered category should persist at the requested position.");
                    Require(viewModel.MoveEntryToCategory(note.Id, "生活"), "A Note should move to an existing category.");
                    Require(note.Category == "生活", "Moved Note should expose the destination category.");
                    Require(viewModel.DeleteCategory("生活"), "Deleting an existing category should succeed.");
                    Require(note.Category == String.Empty, "Deleting a category must preserve its Notes as uncategorized.");
                    Require(!viewModel.CreateCategory("工作", out error) && error != null, "Duplicate category names should be rejected.");
                    Require(viewModel.Flush(), "Category changes should flush successfully.");
                }

                LoadResult loaded = store.Load();
                Require(loaded.Success, "Ordered categories should reload.");
                Require(loaded.State.Categories.Count == 2, "Deleted categories should not return after reload.");
                Require(loaded.State.Categories[0] == "参考" && loaded.State.Categories[1] == "工作", "Custom category order should round-trip.");
                Require(loaded.State.Entries.Count == 1 && loaded.State.Entries[0].Category == String.Empty, "Unified Note category movement should round-trip.");
            });
        }

        private static void ViewModelDeletesRestoresAndPersists()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                var entry = new Entry
                {
                    Title = "待删除条目",
                    Body = "中文正文",
                    Category = "测试"
                };
                state.Categories.Add("测试");
                state.Entries.Add(entry);

                var store = new PortableStore(root);
                using (var viewModel = new MainViewModel(state, store, new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    Require(Object.ReferenceEquals(entry, viewModel.SelectedEntry), "The initial entry should be selected.");
                    Require(Object.ReferenceEquals(entry, viewModel.SoftDeleteSelected()), "Soft delete should return the selected entry.");
                    Require(entry.IsDeleted && entry.DeletedUtc.HasValue, "Soft delete should retain the entry in trash.");

                    viewModel.SelectView(SmartView.Trash);
                    Require(Object.ReferenceEquals(entry, viewModel.SelectedEntry), "Trash view should select the deleted entry.");
                    Require(Object.ReferenceEquals(entry, viewModel.RestoreSelected()), "Restore should return the restored entry.");
                    Require(!entry.IsDeleted && !entry.DeletedUtc.HasValue, "Restore should clear deletion state.");

                    viewModel.SelectView(SmartView.All);
                    viewModel.SelectEntry(entry);
                    viewModel.SoftDeleteSelected();
                    viewModel.SelectView(SmartView.Trash);
                    Require(Object.ReferenceEquals(entry, viewModel.PermanentlyDeleteSelected()), "Permanent delete should remove the selected trash entry.");
                    Require(state.Entries.Count == 0, "Permanent delete should remove the entry from application state.");
                    Require(viewModel.Flush(), "View-model changes should flush successfully.");
                }

                LoadResult loaded = store.Load();
                Require(loaded.Success, "Persisted state should reload after view-model operations.");
                Require(loaded.State.Entries.Count == 0, "Permanent deletion should persist.");
            });
        }

        private static void ViewModelClearsTrashAndPersists()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                var active = new Entry { Title = "保留条目", Body = "仍然存在" };
                var firstDeleted = new Entry { Title = "回收条目一", IsDeleted = true, DeletedUtc = DateTime.UtcNow.AddMinutes(-2) };
                var secondDeleted = new Entry { Title = "回收条目二", IsDeleted = true, DeletedUtc = DateTime.UtcNow.AddMinutes(-1) };
                state.Entries.Add(active);
                state.Entries.Add(firstDeleted);
                state.Entries.Add(secondDeleted);

                var store = new PortableStore(root);
                using (var viewModel = new MainViewModel(state, store, new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    viewModel.SelectView(SmartView.Trash);
                    Require(viewModel.TrashCount == 2, "Trash count should include every deleted entry.");
                    Require(viewModel.ClearTrash() == 2, "Clear trash should report the number of permanently deleted entries.");
                    Require(viewModel.TrashCount == 0, "Trash should be empty after clearing.");
                    Require(state.Entries.Count == 1 && Object.ReferenceEquals(active, state.Entries[0]), "Clear trash must preserve active entries.");
                    Require(viewModel.SelectedEntry == null, "Trash selection should clear when no deleted entries remain.");
                    Require(viewModel.ClearTrash() == 0, "Clearing an empty trash should be a no-op.");
                    Require(viewModel.Flush(), "Cleared trash should flush successfully.");
                }

                LoadResult loaded = store.Load();
                Require(loaded.Success, "State should reload after clearing trash.");
                Require(loaded.State.Entries.Count == 1 && loaded.State.Entries[0].Title == "保留条目", "Only active entries should persist after clearing trash.");
            });
        }

        private static void SingleInstanceRejectsSecondOwner()
        {
            string applicationId = "SeerNote.Tests." + Guid.NewGuid().ToString("N");
            SingleInstanceGuard first;
            SingleInstanceGuard second;
            Require(SingleInstanceGuard.TryAcquire(applicationId, out first), "First instance should acquire ownership.");
            try
            {
                Require(!SingleInstanceGuard.TryAcquire(applicationId, out second), "Second instance should be rejected.");
                Require(second == null, "Rejected second instance should not return a guard.");
            }
            finally
            {
                first.Dispose();
            }
        }

        private static void DirectoryIdentityIgnoresCaseAndTrailingSeparators()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                string identity = SingleInstanceGuard.GetDirectoryIdentity(root);
                string caseAndSeparatorAlias = root.ToUpperInvariant() + Path.DirectorySeparatorChar;
                Require(identity == SingleInstanceGuard.GetDirectoryIdentity(caseAndSeparatorAlias), "Directory identity should ignore Windows case and trailing separators.");
            });
        }

        private static void LockFileRejectsDifferentNamedInstances()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                string dataDirectory = Path.Combine(root, "data");
                Directory.CreateDirectory(dataDirectory);
                string lockFilePath = Path.Combine(dataDirectory, ".seernote.lock");
                SingleInstanceGuard first;
                SingleInstanceGuard second;
                Require(SingleInstanceGuard.TryAcquire("SeerNote.Tests.A." + Guid.NewGuid().ToString("N"), lockFilePath, out first), "First named instance should acquire the shared data lock.");
                try
                {
                    Require(!SingleInstanceGuard.TryAcquire("SeerNote.Tests.B." + Guid.NewGuid().ToString("N"), lockFilePath, out second), "A different mutex identity must still be rejected by the shared data lock.");
                    Require(second == null, "A data-lock conflict should not return a guard.");
                }
                finally
                {
                    first.Dispose();
                }
            });
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            string root = Path.Combine(Path.GetTempPath(), TemporaryDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                action(root);
            }
            finally
            {
                string full = Path.GetFullPath(root);
                string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
                if (full.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith(TemporaryDirectoryPrefix, StringComparison.Ordinal))
                {
                    Directory.Delete(full, true);
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
