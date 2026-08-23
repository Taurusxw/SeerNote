using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using SeerNote.Domain;

namespace SeerNote.Storage
{
    /// <summary>
    /// Portable, single-writer JSON persistence.  Callers only deal with a
    /// complete AppState; temporary files and recovery evidence stay private.
    /// </summary>
    public sealed class PortableStore
    {
        public const int SupportedSchemaVersion = 2;
        private const int MaximumBackupCount = 10;
        private const string NotesFileName = "notes.json";
        private static readonly StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;

        private readonly string _dataDirectory;
        private readonly string _primaryPath;
        private readonly string _backupPath;
        private readonly string _temporaryPath;
        private readonly string _historyDirectory;
        private readonly string _recoveryDirectory;

        public PortableStore(string applicationDirectory)
        {
            if (String.IsNullOrWhiteSpace(applicationDirectory))
            {
                throw new ArgumentException("应用目录不能为空。", "applicationDirectory");
            }

            var root = Path.GetFullPath(applicationDirectory);
            _dataDirectory = Path.Combine(root, "data");
            _primaryPath = Path.Combine(_dataDirectory, NotesFileName);
            _backupPath = _primaryPath + ".bak";
            _temporaryPath = _primaryPath + ".tmp";
            _historyDirectory = Path.Combine(_dataDirectory, "backups");
            _recoveryDirectory = Path.Combine(_dataDirectory, "recovery");
        }

        public LoadResult Load()
        {
            var report = new RecoveryReport();
            var candidates = GetLoadCandidates();
            AppState recoveredState = null;
            string recoveredPath = null;
            Exception finalError = null;
            var sawCandidate = false;

            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }
                sawCandidate = true;

                AppState candidateState;
                Exception error;
                if (TryReadState(candidate, out candidateState, out error))
                {
                    recoveredState = candidateState;
                    recoveredPath = candidate;
                    break;
                }

                finalError = error;
                report.AddDiagnostic(Path.GetFileName(candidate) + " 无法使用：" + error.Message);
            }

            if (recoveredState == null)
            {
                if (!sawCandidate)
                {
                    return new LoadResult(new AppState(), report, null);
                }

                return new LoadResult(null, report, finalError ?? new InvalidDataException("没有可恢复的数据文件。"));
            }

            if (!PathsEqual(recoveredPath, _primaryPath))
            {
                try
                {
                    RestoreRecoveredState(recoveredPath, recoveredState, report);
                }
                catch (Exception error)
                {
                    report.AddDiagnostic("已读取恢复副本，但无法恢复主文件：" + error.Message);
                    return new LoadResult(recoveredState, report, error);
                }
            }

            return new LoadResult(recoveredState, report, null);
        }

        public SaveResult Save(AppState state)
        {
            try
            {
                if (state == null)
                {
                    throw new ArgumentNullException("state");
                }

                EnsureDirectories();
                var snapshot = state.Clone();
                snapshot.SavedUtc = DateTime.UtcNow;
                ValidateState(snapshot);

                var serialized = Serialize(snapshot);
                AtomicFile.WriteAndFlush(_temporaryPath, serialized);
                ValidateSerializedFile(_temporaryPath, snapshot.Entries.Count);
                CreateDailyBackupIfNeeded();
                AtomicFile.Replace(_temporaryPath, _primaryPath, _backupPath);
                TrimBackups();
                return new SaveResult(_primaryPath, snapshot.SavedUtc, null);
            }
            catch (Exception error)
            {
                return new SaveResult(_primaryPath, DateTime.MinValue, error);
            }
        }

        public SaveResult Export(string path, AppState state)
        {
            try
            {
                if (state == null)
                {
                    throw new ArgumentNullException("state");
                }
                if (String.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException("导出路径不能为空。", "path");
                }

                var destination = Path.GetFullPath(path);
                var parent = Path.GetDirectoryName(destination);
                if (String.IsNullOrEmpty(parent))
                {
                    throw new InvalidOperationException("导出路径必须包含目录。");
                }
                Directory.CreateDirectory(parent);

                var snapshot = state.Clone();
                snapshot.SavedUtc = DateTime.UtcNow;
                ValidateState(snapshot);
                var temporary = destination + ".tmp";
                AtomicFile.WriteAndFlush(temporary, Serialize(snapshot));
                ValidateSerializedFile(temporary, snapshot.Entries.Count);
                AtomicFile.Replace(temporary, destination, destination + ".bak");
                return new SaveResult(destination, snapshot.SavedUtc, null);
            }
            catch (Exception error)
            {
                return new SaveResult(path, DateTime.MinValue, error);
            }
        }

        private void RestoreRecoveredState(string sourcePath, AppState state, RecoveryReport report)
        {
            EnsureDirectories();
            if (File.Exists(_primaryPath))
            {
                var preserved = CreateRecoveryPath();
                File.Move(_primaryPath, preserved);
                report.PreservedCorruptPath = preserved;
            }

            AtomicFile.WriteAndFlush(_temporaryPath, Serialize(state));
            ValidateSerializedFile(_temporaryPath, state.Entries.Count);
            AtomicFile.Replace(_temporaryPath, _primaryPath, _backupPath);
            report.Recovered = true;
            report.SourcePath = sourcePath;
        }

        private IEnumerable<string> GetLoadCandidates()
        {
            yield return _primaryPath;
            yield return _backupPath;
            yield return _temporaryPath;

            if (!Directory.Exists(_historyDirectory))
            {
                yield break;
            }

            foreach (var path in Directory.GetFiles(_historyDirectory, "notes-*.json")
                .Where(IsSafeBackupPath)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
            {
                yield return path;
            }
        }

        private void CreateDailyBackupIfNeeded()
        {
            if (!File.Exists(_primaryPath))
            {
                return;
            }

            Directory.CreateDirectory(_historyDirectory);
            var prefix = "notes-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";
            if (Directory.GetFiles(_historyDirectory, prefix + "*.json").Any(IsSafeBackupPath))
            {
                return;
            }

            var backupName = prefix + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".json";
            var backupPath = Path.Combine(_historyDirectory, backupName);
            File.Copy(_primaryPath, backupPath, false);
            AppState backupState;
            Exception error;
            if (!TryReadState(backupPath, out backupState, out error))
            {
                File.Delete(backupPath);
                throw new InvalidDataException("每日备份验证失败。", error);
            }
        }

        private void TrimBackups()
        {
            if (!Directory.Exists(_historyDirectory))
            {
                return;
            }

            var excess = Directory.GetFiles(_historyDirectory, "notes-*.json")
                .Where(IsSafeBackupPath)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .Skip(MaximumBackupCount)
                .ToArray();
            foreach (var path in excess)
            {
                File.Delete(path);
            }
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(_dataDirectory);
            Directory.CreateDirectory(_historyDirectory);
        }

        private string CreateRecoveryPath()
        {
            Directory.CreateDirectory(_recoveryDirectory);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            var candidate = Path.Combine(_recoveryDirectory, "notes-corrupt-" + stamp + ".json");
            var suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(_recoveryDirectory, "notes-corrupt-" + stamp + "-" + suffix.ToString(CultureInfo.InvariantCulture) + ".json");
                suffix++;
            }
            return candidate;
        }

        private static bool TryReadState(string path, out AppState state, out Exception error)
        {
            try
            {
                using (var stream = new MemoryStream(AtomicFile.ReadAllBytes(path), false))
                {
                    var serializer = new DataContractJsonSerializer(typeof(StoredState));
                    state = ((StoredState)serializer.ReadObject(stream)).ToDomain();
                }
                ValidateState(state);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                state = null;
                error = exception;
                return false;
            }
        }

        private static byte[] Serialize(AppState state)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(StoredState));
                serializer.WriteObject(stream, StoredState.FromDomain(state));
                return stream.ToArray();
            }
        }

        private static void ValidateSerializedFile(string path, int expectedEntryCount)
        {
            AppState state;
            Exception error;
            if (!TryReadState(path, out state, out error))
            {
                throw new InvalidDataException("临时保存文件未通过回读验证。", error);
            }
            if (state.Entries.Count != expectedEntryCount)
            {
                throw new InvalidDataException("临时保存文件的条目数量不一致。");
            }
        }

        private static void ValidateState(AppState state)
        {
            if (state == null)
            {
                throw new InvalidDataException("应用状态为空。");
            }
            if (state.SchemaVersion != SupportedSchemaVersion)
            {
                throw new InvalidDataException("不支持的数据版本：" + state.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            }
            state.Validate();
        }

        private bool IsSafeBackupPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var expectedDirectory = Path.GetFullPath(_historyDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(fullPath);
            var name = Path.GetFileName(fullPath);
            if (!String.Equals(parent, expectedDirectory, PathComparison))
            {
                return false;
            }
            if (name == null || name.Length != "notes-YYYYMMDD-HHMMSS.json".Length)
            {
                return false;
            }
            return name.StartsWith("notes-", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal)
                && name.Skip(6).Take(8).All(Char.IsDigit)
                && name[14] == '-'
                && name.Skip(15).Take(6).All(Char.IsDigit);
        }

        private static bool PathsEqual(string left, string right)
        {
            return String.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);
        }
    }
}
