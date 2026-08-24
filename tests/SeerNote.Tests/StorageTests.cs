using System;
using System.IO;
using System.Linq;
using System.Text;
using SeerNote.Domain;
using SeerNote.Storage;

namespace SeerNote.Tests
{
    public static class StorageTests
    {
        private const string TemporaryDirectoryPrefix = "SeerNote.StorageTests-";

        public static void RunAll()
        {
            UnicodeRoundTrip();
            CurrentSchemaPreservesManualEntryOrder();
            LegacySettingsDefaultCloseButtonToExit();
            LegacySchemaMigratesTypesAndCategories();
            SchemaTwoMigratesLegacyDisplayOrder();
            ReplaceCreatesBackup();
            CorruptPrimaryRecoversFromBackup();
            UnsupportedSchemaIsRejected();
            TemporaryFileLockLeavesPrimaryUnchanged();
            PrimaryFileLockLeavesPrimaryLoadable();
            CorruptPrimaryRecoversFromTemporaryFile();
            RecoveryWriteFailureStillReturnsBackupState();
        }

        private static void UnicodeRoundTrip()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = NewState("中文标题 😀", "第一行\r\n第二行：你好，世界 🌏", "工作");
                state.Settings.CloseButtonBehavior = CloseButtonBehavior.MinimizeToTray;
                state.Settings.Theme = AppTheme.Sage;

                Require(store.Save(state).Success, "Unicode save should succeed.");
                var loaded = store.Load();
                Require(loaded.Success, "Unicode load should succeed.");
                Require(loaded.State.Entries.Count == 1, "Unicode entry should remain present.");
                Require(loaded.State.Entries[0].Title == "中文标题 😀", "Unicode title should round-trip.");
                Require(loaded.State.Entries[0].Body == "第一行\r\n第二行：你好，世界 🌏", "Unicode body should round-trip.");
                Require(loaded.State.Settings.CloseButtonBehavior == CloseButtonBehavior.MinimizeToTray, "Close button behavior should round-trip.");
                Require(loaded.State.Settings.Theme == AppTheme.Sage, "Selected theme should round-trip.");

                var json = File.ReadAllText(Path.Combine(root, "data", "notes.json"), Encoding.UTF8);
                Require(json.Contains("\"schemaVersion\":3"), "Persisted contract should own the current lowercase schema member.");
                Require(json.Contains("\"categories\":[\"工作\"]"), "Persisted contract should preserve ordered custom categories.");
                Require(!json.Contains("\"type\":"), "Unified Note storage should no longer emit the legacy memo/prompt type.");
                Require(json.Contains("\"closeButtonBehavior\":\"minimizetotray\""), "Persisted settings should include close button behavior.");
                Require(json.Contains("\"theme\":\"sage\""), "Persisted settings should include the selected theme.");
            });
        }

        private static void CurrentSchemaPreservesManualEntryOrder()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = new AppState();
                DateTime baseline = DateTime.UtcNow.AddHours(-3);
                var first = new Entry { Title = "手工第一", CreatedUtc = baseline, UpdatedUtc = baseline.AddHours(1) };
                var second = new Entry { Title = "手工第二", CreatedUtc = baseline, UpdatedUtc = baseline.AddHours(2) };
                state.Entries.Add(first);
                state.Entries.Add(second);

                Require(store.Save(state).Success, "Schema 3 manual-order fixture should save.");
                LoadResult loaded = store.Load();
                Require(loaded.Success, "Schema 3 manual-order fixture should reload.");
                Require(loaded.State.Entries[0].Id == first.Id && loaded.State.Entries[1].Id == second.Id, "Schema 3 should treat the entries array as authoritative manual order.");
            });
        }

        private static void LegacySettingsDefaultCloseButtonToExit()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                string dataDirectory = Path.Combine(root, "data");
                Directory.CreateDirectory(dataDirectory);
                File.WriteAllText(
                    Path.Combine(dataDirectory, "notes.json"),
                    "{\"schemaVersion\":1,\"savedUtc\":\"2026-08-18T00:00:00.0000000Z\",\"settings\":{\"globalHotkey\":\"Ctrl+Shift+Space\",\"windowBounds\":{\"left\":0,\"top\":0,\"width\":1080,\"height\":720},\"lastSmartView\":\"all\"},\"entries\":[]}",
                    new UTF8Encoding(false));

                LoadResult loaded = new PortableStore(root).Load();
                Require(loaded.Success, "Settings written before close behavior existed should remain loadable. "
                    + (loaded.Error == null ? String.Empty : loaded.Error.GetBaseException().Message));
                Require(loaded.State.Settings.CloseButtonBehavior == CloseButtonBehavior.Exit, "Legacy settings should default the close button to a complete exit.");
                Require(loaded.State.Settings.Theme == AppTheme.Graphite, "Legacy settings should retain the graphite theme by default.");
                Require(loaded.State.SchemaVersion == AppState.CurrentSchemaVersion, "Legacy schema should migrate in memory to the current version.");
            });
        }

        private static void LegacySchemaMigratesTypesAndCategories()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                string dataDirectory = Path.Combine(root, "data");
                Directory.CreateDirectory(dataDirectory);
                string entryId = Guid.NewGuid().ToString("D");
                string json = "{\"schemaVersion\":1,\"savedUtc\":\"2026-08-18T00:00:00.0000000Z\","
                    + "\"settings\":{\"globalHotkey\":\"Ctrl+Shift+Space\",\"windowBounds\":{\"left\":0,\"top\":0,\"width\":1080,\"height\":720},\"lastSmartView\":\"prompt\"},"
                    + "\"entries\":[{\"id\":\"" + entryId + "\",\"type\":\"prompt\",\"title\":\"旧提示词\",\"body\":\"你好 {{姓名}}\",\"category\":\" 工作 \","
                    + "\"isFavorite\":false,\"isDeleted\":false,\"sticky\":{\"isOpen\":false,\"left\":0,\"top\":0,\"width\":360,\"height\":260},"
                    + "\"createdUtc\":\"2026-08-18T00:00:00.0000000Z\",\"updatedUtc\":\"2026-08-18T00:00:00.0000000Z\"}]}";
                File.WriteAllText(Path.Combine(dataDirectory, "notes.json"), json, new UTF8Encoding(false));

                LoadResult loaded = new PortableStore(root).Load();
                Require(loaded.Success, "Schema 1 memo/prompt data should migrate without content loss.");
                Require(loaded.State.SchemaVersion == AppState.CurrentSchemaVersion, "Migrated state should use the current schema.");
                Require(loaded.State.Settings.LastSmartView == SmartView.All, "Legacy memo/prompt navigation should migrate to all Notes.");
                Require(loaded.State.Categories.Count == 1 && loaded.State.Categories[0] == "工作", "Legacy entry categories should become ordered custom categories.");
                Require(loaded.State.Entries.Count == 1 && loaded.State.Entries[0].Body == "你好 {{姓名}}", "Legacy Note body should survive migration.");
            });
        }

        private static void SchemaTwoMigratesLegacyDisplayOrder()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                string dataDirectory = Path.Combine(root, "data");
                Directory.CreateDirectory(dataDirectory);
                string normalOld = StoredEntryJson("普通旧", false, false, "2026-08-18T01:00:00.0000000Z", null);
                string deletedOld = StoredEntryJson("回收旧", false, true, "2026-08-18T02:00:00.0000000Z", "2026-08-18T02:00:00.0000000Z");
                string favoriteOld = StoredEntryJson("收藏旧", true, false, "2026-08-18T03:00:00.0000000Z", null);
                string normalNew = StoredEntryJson("普通新", false, false, "2026-08-18T04:00:00.0000000Z", null);
                string deletedNew = StoredEntryJson("回收新", false, true, "2026-08-18T05:00:00.0000000Z", "2026-08-18T05:00:00.0000000Z");
                string favoriteNew = StoredEntryJson("收藏新", true, false, "2026-08-18T06:00:00.0000000Z", null);
                string json = "{\"schemaVersion\":2,\"savedUtc\":\"2026-08-18T07:00:00.0000000Z\","
                    + "\"settings\":{\"globalHotkey\":\"Ctrl+Shift+Space\",\"windowBounds\":{\"left\":0,\"top\":0,\"width\":1080,\"height\":720},\"lastSmartView\":\"all\"},"
                    + "\"categories\":[],\"entries\":[" + normalOld + "," + deletedOld + "," + favoriteOld + "," + normalNew + "," + deletedNew + "," + favoriteNew + "]}";
                File.WriteAllText(Path.Combine(dataDirectory, "notes.json"), json, new UTF8Encoding(false));

                LoadResult loaded = new PortableStore(root).Load();
                Require(loaded.Success, "Schema 2 data should migrate to manual ordering.");
                string[] titles = loaded.State.Entries.Select(entry => entry.Title).ToArray();
                Require(titles.SequenceEqual(new[] { "收藏新", "收藏旧", "普通新", "普通旧", "回收新", "回收旧" }), "Schema 2 migration should freeze the legacy first-display order before schema 3 takes ownership.");
                Require(loaded.State.SchemaVersion == AppState.CurrentSchemaVersion, "Schema 2 migration should advance to the current schema in memory.");
            });
        }

        private static string StoredEntryJson(string title, bool favorite, bool deleted, string updatedUtc, string deletedUtc)
        {
            return "{\"id\":\"" + Guid.NewGuid().ToString("D") + "\",\"title\":\"" + title + "\",\"body\":\"\",\"category\":\"\","
                + "\"isFavorite\":" + favorite.ToString().ToLowerInvariant() + ",\"isDeleted\":" + deleted.ToString().ToLowerInvariant() + ","
                + "\"sticky\":{\"isOpen\":false,\"left\":0,\"top\":0,\"width\":360,\"height\":260},"
                + "\"createdUtc\":\"2026-08-18T00:00:00.0000000Z\",\"updatedUtc\":\"" + updatedUtc + "\""
                + (deletedUtc == null ? String.Empty : ",\"deletedUtc\":\"" + deletedUtc + "\"") + "}";
        }

        private static void ReplaceCreatesBackup()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = NewState("初始标题", "初始正文", "测试");
                Require(store.Save(state).Success, "Initial save should succeed.");
                state.Entries[0].Title = "替换后的标题";
                state.Entries[0].UpdatedUtc = DateTime.UtcNow;
                Require(store.Save(state).Success, "Replacement save should succeed.");

                var backup = Path.Combine(root, "data", "notes.json.bak");
                Require(File.Exists(backup), "Replacing a primary file should create notes.json.bak.");
                Require(File.ReadAllText(backup, Encoding.UTF8).Contains("初始标题"), "The replacement backup should contain the prior committed state.");
                Require(store.Load().State.Entries[0].Title == "替换后的标题", "Primary should contain the latest committed state.");
            });
        }

        private static void CorruptPrimaryRecoversFromBackup()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = NewState("可恢复版本", "正文", "测试");
                Require(store.Save(state).Success, "Initial save should succeed.");
                state.Entries[0].Title = "最新版本";
                state.Entries[0].UpdatedUtc = DateTime.UtcNow;
                Require(store.Save(state).Success, "Second save should produce a backup.");

                var primary = Path.Combine(root, "data", "notes.json");
                File.WriteAllText(primary, "{not valid json", Encoding.UTF8);
                var recovered = store.Load();

                Require(recovered.Success, "A valid backup should recover a corrupt primary.");
                Require(recovered.Recovery.Recovered, "Recovery report should record backup restoration.");
                Require(recovered.State.Entries[0].Title == "可恢复版本", "Recovery should load the last valid backup.");
                Require(!String.IsNullOrEmpty(recovered.Recovery.PreservedCorruptPath) && File.Exists(recovered.Recovery.PreservedCorruptPath), "The corrupt primary must be preserved as evidence.");
            });
        }

        private static void UnsupportedSchemaIsRejected()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var dataDirectory = Path.Combine(root, "data");
                Directory.CreateDirectory(dataDirectory);
                File.WriteAllText(Path.Combine(dataDirectory, "notes.json"),
                    "{\"schemaVersion\":99,\"savedUtc\":\"2026-08-18T00:00:00.0000000Z\",\"settings\":{\"globalHotkey\":\"Ctrl+Shift+Space\",\"windowBounds\":{\"left\":0,\"top\":0,\"width\":1080,\"height\":720},\"lastSmartView\":\"all\"},\"entries\":[]}",
                    new UTF8Encoding(false));

                var loaded = new PortableStore(root).Load();
                Require(!loaded.Success && loaded.State == null, "Unsupported schema must not be silently loaded as current data.");
                Require(loaded.Error != null, "Unsupported schema should provide an error.");
            });
        }

        private static void TemporaryFileLockLeavesPrimaryUnchanged()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = NewState("稳定主文件", "旧正文", "测试");
                Require(store.Save(state).Success, "Initial save should succeed.");

                var primary = Path.Combine(root, "data", "notes.json");
                var before = File.ReadAllBytes(primary);
                state.Entries[0].Title = "不应提交的修改";
                state.Entries[0].UpdatedUtc = DateTime.UtcNow;
                var temporary = primary + ".tmp";
                using (new FileStream(temporary, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var failed = store.Save(state);
                    Require(!failed.Success, "Saving while notes.json.tmp is locked should fail.");
                }

                Require(before.SequenceEqual(File.ReadAllBytes(primary)), "A temporary-file write failure must leave primary bytes unchanged.");
                var loaded = store.Load();
                Require(loaded.Success && loaded.State.Entries[0].Title == "稳定主文件", "A temporary-file write failure must leave the old primary loadable.");
            });
        }

        private static void PrimaryFileLockLeavesPrimaryLoadable()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = NewState("替换前", "旧正文", "测试");
                Require(store.Save(state).Success, "Initial save should succeed.");

                var primary = Path.Combine(root, "data", "notes.json");
                var before = File.ReadAllBytes(primary);
                state.Entries[0].Title = "不应替换";
                state.Entries[0].UpdatedUtc = DateTime.UtcNow;
                using (new FileStream(primary, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var failed = store.Save(state);
                    Require(!failed.Success, "Saving while notes.json is locked should fail at replacement.");
                }

                Require(before.SequenceEqual(File.ReadAllBytes(primary)), "A replacement failure must leave primary bytes unchanged.");
                var loaded = store.Load();
                Require(loaded.Success && loaded.State.Entries[0].Title == "替换前", "A replacement failure must leave the old primary loadable.");
            });
        }

        private static void CorruptPrimaryRecoversFromTemporaryFile()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = NewState("来自临时文件", "可恢复正文", "测试");
                Require(store.Save(state).Success, "Initial save should succeed.");

                var primary = Path.Combine(root, "data", "notes.json");
                var backup = primary + ".bak";
                var temporary = primary + ".tmp";
                File.Copy(primary, temporary, true);
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
                File.WriteAllText(primary, "{corrupt primary", Encoding.UTF8);

                var recovered = store.Load();
                Require(recovered.Success, "A valid temporary file should recover a corrupt primary when no backup is available.");
                Require(recovered.Recovery.Recovered && String.Equals(recovered.Recovery.SourcePath, temporary, StringComparison.OrdinalIgnoreCase), "Recovery report should identify notes.json.tmp as the source.");
                Require(recovered.State.Entries[0].Title == "来自临时文件", "Temporary-file recovery should return its valid state.");
                Require(File.Exists(primary) && new PortableStore(root).Load().Success, "Temporary-file recovery should write a valid primary file.");
            });
        }

        private static void RecoveryWriteFailureStillReturnsBackupState()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var store = new PortableStore(root);
                var state = NewState("备份版本", "旧正文", "测试");
                Require(store.Save(state).Success, "Initial save should succeed.");
                state.Entries[0].Title = "损坏前的新版本";
                state.Entries[0].UpdatedUtc = DateTime.UtcNow;
                Require(store.Save(state).Success, "Second save should create a valid backup.");

                var primary = Path.Combine(root, "data", "notes.json");
                var backup = primary + ".bak";
                var temporary = primary + ".tmp";
                File.WriteAllText(primary, "{corrupt primary", Encoding.UTF8);
                using (new FileStream(temporary, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var partiallyRecovered = store.Load();
                    Require(partiallyRecovered.State != null && partiallyRecovered.State.Entries[0].Title == "备份版本", "Recovery write failure must still return the usable state read from backup.");
                    Require(partiallyRecovered.Error != null, "Recovery write failure should be reported to the caller.");
                    Require(File.Exists(backup), "Backup must remain on disk while recovery write is blocked.");
                    var backupRead = new PortableStore(root).Load();
                    Require(backupRead.State != null && backupRead.State.Entries[0].Title == "备份版本", "Backup must remain readable while recovery write is blocked.");
                }

                var restored = store.Load();
                Require(restored.Success && restored.Recovery.Recovered, "After releasing notes.json.tmp, the valid backup should restore primary data.");
                Require(restored.State.Entries[0].Title == "备份版本", "The delayed recovery should preserve backup content.");
            });
        }

        private static AppState NewState(string title, string body, string category)
        {
            var now = DateTime.UtcNow;
            var state = new AppState();
            if (!String.IsNullOrWhiteSpace(category))
            {
                state.Categories.Add(category.Trim());
            }
            state.Entries.Add(new Entry
            {
                Title = title,
                Body = body,
                Category = category,
                CreatedUtc = now,
                UpdatedUtc = now
            });
            return state;
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            var path = Path.Combine(Path.GetTempPath(), TemporaryDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            try
            {
                action(path);
            }
            finally
            {
                DeleteDedicatedTemporaryDirectory(path);
            }
        }

        private static void DeleteDedicatedTemporaryDirectory(string path)
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var expectedPrefix = temporaryRoot + TemporaryDirectoryPrefix;
            if (!fullPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(fullPath).Length != TemporaryDirectoryPrefix.Length + 32)
            {
                throw new InvalidOperationException("Refusing to delete a directory outside the dedicated storage-test prefix.");
            }
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
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
