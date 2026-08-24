using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            ViewModelReordersFilteredNotesAndPersistsHiddenSlots();
            ViewModelClearsTrashAndPersists();
            ClearTrashCompactsInPlaceAndPreservesNoOpState();
            ViewModelManagesOrderedCategoriesAndUnifiedNotes();
            ViewModelUpdatesCloseButtonBehaviorAndPersists();
            FilteredEntriesReuseStableSnapshotsAndInvalidate();
            NavigationSnapshotsReuseUntilNavigationChanges();
            EntrySelectionUsesNarrowNotification();
            StatusAnnouncementsStayActionable();
            SingleInstanceRejectsSecondOwner();
            DirectoryIdentityIgnoresCaseAndTrailingSeparators();
            LockFileRejectsDifferentNamedInstances();
        }

        private static void ViewModelReordersFilteredNotesAndPersistsHiddenSlots()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("隐藏");
                state.Categories.Add("显示");
                var hiddenFirst = new Entry { Title = "隐藏一", Category = "隐藏" };
                var visibleFirst = new Entry { Title = "显示一", Category = "显示" };
                var hiddenSecond = new Entry { Title = "隐藏二", Category = "隐藏" };
                var visibleSecond = new Entry { Title = "显示二", Category = "显示" };
                var favorite = new Entry { Title = "收藏", Category = "显示", IsFavorite = true };
                state.Entries.Add(hiddenFirst);
                state.Entries.Add(visibleFirst);
                state.Entries.Add(hiddenSecond);
                state.Entries.Add(visibleSecond);
                state.Entries.Add(favorite);
                var store = new PortableStore(root);

                using (var viewModel = new MainViewModel(state, store, new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    viewModel.SelectCategory("显示");
                    viewModel.SelectEntry(visibleSecond);
                    IList<Entry> before = viewModel.GetFilteredEntries();
                    Require(before.SequenceEqual(new[] { favorite, visibleFirst, visibleSecond }), "The filtered reorder fixture should pin favorites before the normal group.");
                    Require(viewModel.ReorderEntry(visibleSecond.Id, visibleFirst.Id, false), "A visible Note should move before another Note in its group.");
                    IList<Entry> after = viewModel.GetFilteredEntries();
                    Require(after.SequenceEqual(new[] { favorite, visibleSecond, visibleFirst }), "The filtered projection should publish the requested manual order.");
                    Require(Object.ReferenceEquals(viewModel.SelectedEntry, visibleSecond), "Reordering should preserve the selected Note identity.");
                    Require(Object.ReferenceEquals(state.Entries[0], hiddenFirst) && Object.ReferenceEquals(state.Entries[1], visibleSecond)
                        && Object.ReferenceEquals(state.Entries[2], hiddenSecond) && Object.ReferenceEquals(state.Entries[3], visibleFirst), "Hidden Notes should retain their slots while visible slots are reordered.");
                    Require(!viewModel.ReorderEntry(visibleFirst.Id, favorite.Id, false), "Reordering across the favorite boundary should be rejected.");
                    Require(viewModel.Flush(), "Manual Note order should flush successfully.");
                }

                LoadResult loaded = store.Load();
                Require(loaded.Success, "Manual Note order should reload successfully.");
                Require(loaded.State.Entries.Select(entry => entry.Title).SequenceEqual(new[] { "隐藏一", "显示二", "隐藏二", "显示一", "收藏" }), "Persisted ordering should retain hidden slots and the reordered visible Notes.");
            });
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

        private static void FilteredEntriesReuseStableSnapshotsAndInvalidate()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("工作");
                state.Categories.Add("资料");
                var first = new Entry { Title = "Alpha 工作", Body = "共同关键词", Category = "工作", UpdatedUtc = DateTime.UtcNow };
                var second = new Entry { Title = "Alpha 资料", Body = "共同关键词", Category = "资料", UpdatedUtc = DateTime.UtcNow.AddMinutes(-1) };
                state.Entries.Add(first);
                state.Entries.Add(second);

                using (var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    IList<Entry> initial = viewModel.GetFilteredEntries();
                    Require(Object.ReferenceEquals(initial, viewModel.GetFilteredEntries()), "Unchanged filter state should reuse its read-only result snapshot.");

                    viewModel.SetSearchText("Alpha");
                    IList<Entry> searched = viewModel.GetFilteredEntries();
                    Require(searched.Count == 2 && !Object.ReferenceEquals(initial, searched), "Changing the query should produce a fresh, correct result snapshot.");
                    Require(Object.ReferenceEquals(searched, viewModel.GetFilteredEntries()), "Repeated reads of one query should reuse the computed snapshot.");

                    viewModel.SelectCategory("工作");
                    IList<Entry> category = viewModel.GetFilteredEntries();
                    Require(category.Count == 1 && Object.ReferenceEquals(first, category[0]), "Category filtering should preserve the matching Note after pre-filtering.");
                    Require(Object.ReferenceEquals(category, viewModel.GetFilteredEntries()), "Repeated category reads should reuse the computed snapshot.");

                    Require(viewModel.MoveEntryToCategory(first.Id, "资料"), "The cache fixture should move the selected Note to another category.");
                    IList<Entry> afterMove = viewModel.GetFilteredEntries();
                    Require(afterMove.Count == 0 && !Object.ReferenceEquals(category, afterMove), "A content mutation should invalidate the snapshot before selection is reconciled.");
                    Require(viewModel.SelectedEntry == null, "Selection reconciliation should observe the invalidated category result.");
                }
            });
        }

        private static void NavigationSnapshotsReuseUntilNavigationChanges()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("工作");
                state.Categories.Add("资料");
                var active = new Entry { Title = "Alpha", Body = "初始正文", Category = "工作", UpdatedUtc = DateTime.UtcNow };
                var deleted = new Entry { Title = "已删除", Category = "资料", IsDeleted = true, DeletedUtc = DateTime.UtcNow };
                state.Entries.Add(active);
                state.Entries.Add(deleted);

                using (var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    NavigationSnapshot snapshot = viewModel.GetNavigationSnapshot();
                    Require(Object.ReferenceEquals(snapshot, viewModel.GetNavigationSnapshot()), "Stable navigation reads should reuse one immutable snapshot.");
                    Require(snapshot.AllCount == 1 && snapshot.TrashCount == 1 && snapshot.CategoryCounts["工作"] == 1, "The initial cached navigation snapshot should preserve count policy.");

                    viewModel.UpdateSelectedBody("正文变化不影响导航计数");
                    Require(Object.ReferenceEquals(snapshot, viewModel.GetNavigationSnapshot()), "Text edits should retain the navigation snapshot.");
                    active.Body = "置顶小窗正文变化";
                    viewModel.NotifyExternalEntryChanged(active);
                    Require(Object.ReferenceEquals(snapshot, viewModel.GetNavigationSnapshot()), "Known external body or sticky changes should retain the navigation snapshot.");
                    viewModel.SetSearchText("Alpha");
                    viewModel.SelectCategory("工作");
                    viewModel.SelectView(SmartView.All);
                    Require(Object.ReferenceEquals(snapshot, viewModel.GetNavigationSnapshot()), "Search and navigation selection changes should retain count content.");

                    Entry created = viewModel.CreateEntry();
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Creating a Note should invalidate navigation counts.");
                    Require(snapshot.AllCount == 2, "Creating an active Note should increase the all count.");

                    viewModel.ToggleFavorite();
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Changing favorite membership should invalidate navigation counts.");
                    Require(snapshot.FavoriteCount == 1, "Favoriting the created Note should increase the favorite count.");

                    Require(viewModel.MoveEntryToCategory(created.Id, "资料"), "The navigation cache fixture should move the created Note.");
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Moving a Note should invalidate category counts.");
                    Require(snapshot.CategoryCounts["工作"] == 1 && snapshot.CategoryCounts["资料"] == 1, "Moved Note counts should appear in the destination category.");

                    viewModel.SelectEntry(created);
                    viewModel.SoftDeleteSelected();
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Soft deletion should invalidate active, favorite and trash counts.");
                    Require(snapshot.AllCount == 1 && snapshot.FavoriteCount == 0 && snapshot.TrashCount == 2, "Soft deletion should move the Note between navigation scopes.");

                    viewModel.SelectEntry(created);
                    viewModel.RestoreSelected();
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Restoring a Note should invalidate navigation counts.");
                    Require(snapshot.AllCount == 2 && snapshot.FavoriteCount == 1 && snapshot.TrashCount == 1, "Restoration should return the Note to active navigation scopes.");

                    viewModel.SelectEntry(created);
                    viewModel.SoftDeleteSelected();
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Repeated soft deletion should still invalidate navigation counts.");
                    viewModel.SelectEntry(created);
                    viewModel.PermanentlyDeleteSelected();
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Permanent deletion should invalidate trash counts.");
                    Require(snapshot.TrashCount == 1, "Permanent deletion should remove only the selected trash Note.");

                    viewModel.ClearTrash();
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Clearing trash should invalidate its count.");
                    Require(snapshot.TrashCount == 0, "Clearing trash should publish an empty trash count.");

                    string error;
                    Require(viewModel.CreateCategory("归档", out error), error);
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Creating a category should invalidate navigation order.");
                    Require(viewModel.RenameCategory("归档", "参考", out error), error);
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Renaming a category should invalidate navigation content.");
                    Require(viewModel.ReorderCategory("参考", "工作", false), "The navigation cache fixture should reorder categories.");
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Reordering categories should invalidate navigation order.");
                    Require(viewModel.DeleteCategory("参考"), "The navigation cache fixture should delete the renamed category.");
                    snapshot = RequireFreshNavigationSnapshot(viewModel, snapshot, "Deleting a category should invalidate navigation content.");
                    Require(snapshot.Categories.Count == 2, "Category invalidation should preserve the final ordered category set.");
                }
            });
        }

        private static NavigationSnapshot RequireFreshNavigationSnapshot(MainViewModel viewModel, NavigationSnapshot previous, string message)
        {
            NavigationSnapshot current = viewModel.GetNavigationSnapshot();
            Require(!Object.ReferenceEquals(previous, current), message);
            Require(Object.ReferenceEquals(current, viewModel.GetNavigationSnapshot()), "A rebuilt navigation snapshot should remain stable until the next relevant mutation.");
            return current;
        }

        private static void EntrySelectionUsesNarrowNotification()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                var first = new Entry { Title = "第一条" };
                var second = new Entry { Title = "第二条", UpdatedUtc = first.UpdatedUtc.AddMinutes(-1) };
                state.Entries.Add(first);
                state.Entries.Add(second);

                using (var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    int contentChanges = 0;
                    int selectionChanges = 0;
                    viewModel.ContentChanged += delegate { contentChanges++; };
                    viewModel.SelectedEntryChanged += delegate { selectionChanges++; };

                    viewModel.SelectEntry(second);
                    Require(Object.ReferenceEquals(second, viewModel.SelectedEntry), "Selecting a Note should update the current entry.");
                    Require(selectionChanges == 1 && contentChanges == 0, "Pure selection should use the narrow notification without reporting a content mutation.");

                    viewModel.SelectEntry(second);
                    Require(selectionChanges == 1 && contentChanges == 0, "Selecting the current Note again should remain a no-op.");

                    viewModel.UpdateSelectedBody("正文发生变化");
                    Require(selectionChanges == 1 && contentChanges == 1, "Editing the selected Note should continue through the content-change path.");
                }
            });
        }

        private static void StatusAnnouncementsStayActionable()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Entries.Add(new Entry { Title = "状态反馈", Body = "初始正文" });
                using (var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    Require(!viewModel.StatusShouldAnnounce && viewModel.StatusRevision == 0, "Initial status should not create an unsolicited announcement.");

                    viewModel.ReportStatus("已完成用户动作。", false);
                    int actionRevision = viewModel.StatusRevision;
                    Require(viewModel.StatusShouldAnnounce && actionRevision > 0, "Explicit user-action feedback should be eligible for a polite announcement.");

                    viewModel.UpdateSelectedBody("触发自动保存但不制造播报噪声");
                    Require(!viewModel.StatusShouldAnnounce && viewModel.StatusRevision > actionRevision, "Routine autosave transitions should suppress live announcements.");

                    viewModel.ReportStatus("保存失败：请重试。", true);
                    Require(viewModel.StatusShouldAnnounce && viewModel.StatusIsError, "Actionable failures should remain eligible for an assertive announcement.");
                }
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
                    bool selectionWasReconciledBeforeStatus = false;
                    viewModel.StatusChanged += delegate { selectionWasReconciledBeforeStatus = viewModel.SelectedEntry == null; };
                    Require(viewModel.ClearTrash() == 2, "Clear trash should report the number of permanently deleted entries.");
                    Require(selectionWasReconciledBeforeStatus, "Clear trash status observers should not see a removed Note as the current selection.");
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

        private static void ClearTrashCompactsInPlaceAndPreservesNoOpState()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                var activeFirst = new Entry { Title = "保留一", UpdatedUtc = DateTime.UtcNow.AddMinutes(-2) };
                var deletedFirst = new Entry { Title = "删除一", IsDeleted = true, DeletedUtc = DateTime.UtcNow.AddMinutes(-2) };
                var activeSecond = new Entry { Title = "保留二", UpdatedUtc = DateTime.UtcNow.AddMinutes(-1) };
                var deletedSecond = new Entry { Title = "删除二", IsDeleted = true, DeletedUtc = DateTime.UtcNow.AddMinutes(-1) };
                state.Entries.Add(activeFirst);
                state.Entries.Add(null);
                state.Entries.Add(deletedFirst);
                state.Entries.Add(activeSecond);
                state.Entries.Add(deletedSecond);
                List<Entry> entries = state.Entries;

                using (var viewModel = new MainViewModel(state, new PortableStore(root), new ClipboardService(), Dispatcher.CurrentDispatcher))
                {
                    viewModel.SelectEntry(activeSecond);
                    IList<Entry> filteredBefore = viewModel.GetFilteredEntries();
                    NavigationSnapshot navigationBefore = viewModel.GetNavigationSnapshot();
                    int contentChanges = 0;
                    int selectionChanges = 0;
                    int statusChanges = 0;
                    viewModel.ContentChanged += delegate { contentChanges++; };
                    viewModel.SelectedEntryChanged += delegate { selectionChanges++; };
                    viewModel.StatusChanged += delegate { statusChanges++; };

                    Require(viewModel.ClearTrash() == 2, "Clear trash should report every removed non-null deleted Note.");
                    Require(Object.ReferenceEquals(entries, state.Entries), "Clear trash should compact the existing Entry list instead of replacing it.");
                    Require(state.Entries.Count == 3 && Object.ReferenceEquals(state.Entries[0], activeFirst) && state.Entries[1] == null && Object.ReferenceEquals(state.Entries[2], activeSecond), "Clear trash should preserve active and null survivors in their original relative order.");
                    Require(Object.ReferenceEquals(viewModel.SelectedEntry, activeSecond), "Clearing trash should preserve a selected active Note that remains visible.");
                    Require(contentChanges == 1 && selectionChanges == 0 && statusChanges == 1, "A nonempty clear should publish one content/status change without a separate selection event.");
                    Require(viewModel.HasUnsavedChanges && viewModel.StatusText == "尚未保存" && !viewModel.StatusIsError && !viewModel.StatusShouldAnnounce, "A nonempty clear should retain the existing dirty, non-announcing save status contract.");
                    IList<Entry> filteredAfter = viewModel.GetFilteredEntries();
                    NavigationSnapshot navigationAfter = viewModel.GetNavigationSnapshot();
                    Require(!Object.ReferenceEquals(filteredBefore, filteredAfter) && !Object.ReferenceEquals(navigationBefore, navigationAfter), "A nonempty clear should invalidate filtered and navigation caches.");
                    Require(navigationAfter.TrashCount == 0, "A nonempty clear should rebuild an empty trash count.");

                    int statusRevision = viewModel.StatusRevision;
                    Require(viewModel.ClearTrash() == 0, "Clearing an already empty trash should remain a no-op.");
                    Require(Object.ReferenceEquals(filteredAfter, viewModel.GetFilteredEntries()) && Object.ReferenceEquals(navigationAfter, viewModel.GetNavigationSnapshot()), "An empty clear should retain filtered and navigation cache identities.");
                    Require(contentChanges == 1 && selectionChanges == 0 && statusChanges == 1 && viewModel.StatusRevision == statusRevision, "An empty clear should not publish content, selection or status changes.");
                }
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
