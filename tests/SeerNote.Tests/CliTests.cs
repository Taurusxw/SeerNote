using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SeerNote.Agent;
using SeerNote.Cli;
using SeerNote.Domain;
using SeerNote.Platform;
using SeerNote.Storage;

namespace SeerNote.Tests
{
    public static class CliTests
    {
        private const string TemporaryDirectoryPrefix = "SeerNote.CliTests-";

        public static void RunAll()
        {
            SchemaAndLifecycleUseStableJsonContracts();
            MutationsMaintainManualGroupOrder();
            InvalidRequestsUseStructuredErrorsAndExitCodes();
            WorkspaceLockConflictIsReportedWithoutReadingData();
            AgentNotePayloadUsesCanonicalIdentifiersAndUtcTimestamps();
        }

        private static void MutationsMaintainManualGroupOrder()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                var favorite = new Entry { Title = "原收藏", IsFavorite = true };
                var normal = new Entry { Title = "原普通" };
                var deleted = new Entry { Title = "原回收", IsDeleted = true, DeletedUtc = DateTime.UtcNow };
                state.Entries.Add(favorite);
                state.Entries.Add(normal);
                state.Entries.Add(deleted);
                Require(new PortableStore(root).Save(state).Success, "CLI order fixture should save.");

                CliResult created = Run(root, new[] { "create", "--title", "新普通" });
                RequireSuccessJson(created, "create");
                Guid createdId = Guid.Parse(ExtractId(created.Output));
                AppState afterCreate = new PortableStore(root).Load().State;
                IList<Entry> active = EntrySearch.Filter(afterCreate.Entries, null, SmartView.All);
                Require(active.Select(entry => entry.Title).SequenceEqual(new[] { "原收藏", "新普通", "原普通" }), "CLI create should place a Note at the start of its non-favorite group.");

                CliResult favorited = Run(root, new[] { "update", "--id", normal.Id.ToString("D"), "--favorite", "true" });
                RequireSuccessJson(favorited, "update");
                AppState afterFavorite = new PortableStore(root).Load().State;
                active = EntrySearch.Filter(afterFavorite.Entries, null, SmartView.All);
                Require(active.Select(entry => entry.Title).SequenceEqual(new[] { "原普通", "原收藏", "新普通" }), "CLI favorite changes should place a Note at the start of its new pinned group.");

                CliResult removed = Run(root, new[] { "delete", "--id", createdId.ToString("D") });
                RequireSuccessJson(removed, "delete");
                AppState afterDelete = new PortableStore(root).Load().State;
                IList<Entry> trash = EntrySearch.Filter(afterDelete.Entries, null, SmartView.Trash);
                Require(trash.Select(entry => entry.Title).SequenceEqual(new[] { "新普通", "原回收" }), "CLI delete should place a Note at the start of the recycle-bin group.");

                CliResult restored = Run(root, new[] { "restore", "--id", createdId.ToString("D") });
                RequireSuccessJson(restored, "restore");
                AppState afterRestore = new PortableStore(root).Load().State;
                active = EntrySearch.Filter(afterRestore.Entries, null, SmartView.All);
                Require(active.Select(entry => entry.Title).SequenceEqual(new[] { "原普通", "原收藏", "新普通" }), "CLI restore should place a Note at the start of its active group without disturbing favorites.");
            });
        }

        private static void SchemaAndLifecycleUseStableJsonContracts()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("工作");
                Require(new PortableStore(root).Save(state).Success, "CLI lifecycle fixture should save.");

                CliResult schema = Run(root, new[] { "schema" });
                RequireSuccessJson(schema, "schema");
                Require(schema.Output.Contains("\"contract\":\"seernote.cli.v1\""), "Schema should identify the CLI contract.");
                Require(schema.Output.Contains("\"noteContract\":\"seernote.note.v1\""), "Schema should identify the Note payload contract.");
                Require(schema.Output.Contains("\"permanentDeleteAvailable\":false"), "Schema should make permanent deletion unavailable.");

                CliResult categories = Run(root, new[] { "categories" });
                RequireSuccessJson(categories, "categories");
                Require(categories.Output.Contains("\"categories\":[\"工作\"]"), "Categories should return the ordered workspace categories.");

                CliResult created = Run(
                    root,
                    new[] { "create", "--title", "智能体工作流", "--body-stdin", "--category=工作", "--favorite" },
                    "第一行\n第二行");
                RequireSuccessJson(created, "create");
                Require(created.Output.Contains("\"action\":\"created\""), "Create should report its action.");
                Require(created.Output.Contains("\"schema\":\"seernote.note.v1\""), "Create should return the stable Note payload.");
                Require(created.Output.Contains("\"favorite\":true"), "A bare --favorite flag should create a favorite Note.");
                string id = ExtractId(created.Output);

                CliResult list = Run(root, new[] { "list", "--query", "智能体", "--category", "工作", "--limit", "10" });
                RequireSuccessJson(list, "list");
                Require(list.Output.Contains(id) && list.Output.Contains("\"count\":1") && list.Output.Contains("\"total\":1"), "List should search and filter the created Note.");

                CliResult get = Run(root, new[] { "get", "--id=" + id });
                RequireSuccessJson(get, "get");
                Require(get.Output.Contains("第一行\\u000a第二行") || get.Output.Contains("第一行\\n第二行"), "Get should preserve body text read from stdin.");

                CliResult updated = Run(root, new[] { "update", "--id", id, "--title=更新后的 Note", "--body", "更新正文", "--favorite", "false" });
                RequireSuccessJson(updated, "update");
                Require(updated.Output.Contains("\"action\":\"updated\"") && updated.Output.Contains("更新后的 Note") && updated.Output.Contains("\"favorite\":false"), "Update should return the changed Note.");

                CliResult deleted = Run(root, new[] { "delete", "--id", id });
                RequireSuccessJson(deleted, "delete");
                Require(deleted.Output.Contains("\"action\":\"deleted\"") && deleted.Output.Contains("\"deleted\":true"), "Delete should move a Note to the recycle bin.");

                CliResult trash = Run(root, new[] { "list", "--view", "trash" });
                RequireSuccessJson(trash, "list");
                Require(trash.Output.Contains(id) && trash.Output.Contains("\"deleted\":true"), "Trash listing should include the deleted Note.");

                CliResult restored = Run(root, new[] { "restore", "--id", id });
                RequireSuccessJson(restored, "restore");
                Require(restored.Output.Contains("\"action\":\"restored\"") && restored.Output.Contains("\"deleted\":false"), "Restore should return the Note to the active collection.");

                LoadResult persisted = new PortableStore(root).Load();
                Require(persisted.Success && persisted.State.Entries.Count == 1, "CLI mutations should remain loadable through PortableStore.");
                Entry note = persisted.State.Entries[0];
                Require(note.Id.ToString("D") == id && note.Title == "更新后的 Note" && note.Body == "更新正文" && !note.IsDeleted, "CLI lifecycle changes should persist without losing identity or content.");
            });
        }

        private static void InvalidRequestsUseStructuredErrorsAndExitCodes()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                var state = new AppState();
                state.Categories.Add("已存在");
                state.Entries.Add(new Entry { Title = "测试", Body = "正文" });
                Require(new PortableStore(root).Save(state).Success, "CLI error fixture should save.");
                string id = state.Entries[0].Id.ToString("D");

                RequireFailure(Run(root, new[] { "unknown" }), CliExitCodes.UsageError, "unknown_command");
                RequireFailure(Run(root, new[] { "get", "--id", id, "--extra", "value" }), CliExitCodes.UsageError, "unknown_option");
                RequireFailure(Run(root, new[] { "get", "--id", "not-a-uuid" }), CliExitCodes.ValidationError, "invalid_id");
                RequireFailure(Run(root, new[] { "create", "--title", "分类错误", "--category", "不存在" }), CliExitCodes.ValidationError, "unknown_category");
                RequireFailure(Run(root, new[] { "update", "--id", id }), CliExitCodes.ValidationError, "no_changes");
                RequireFailure(Run(root, new[] { "create", "--title", "冲突正文", "--body", "a", "--body-stdin" }, "b"), CliExitCodes.ValidationError, "multiple_body_sources");
                RequireFailure(Run(root, new[] { "get", "--id", Guid.NewGuid().ToString("D") }), CliExitCodes.NotFound, "note_not_found");
            });
        }

        private static void WorkspaceLockConflictIsReportedWithoutReadingData()
        {
            WithTemporaryDirectory(delegate(string root)
            {
                string dataDirectory = Path.Combine(root, "data");
                Directory.CreateDirectory(dataDirectory);
                string lockPath = Path.Combine(dataDirectory, ".seernote.lock");
                string identity = "SeerNote.CliTests.Holder|" + SingleInstanceGuard.GetDirectoryIdentity(root);
                SingleInstanceGuard holder;
                Require(SingleInstanceGuard.TryAcquire(identity, lockPath, out holder), "The CLI lock fixture should acquire the workspace.");
                using (holder)
                {
                    RequireFailure(Run(root, new[] { "categories" }), CliExitCodes.WorkspaceBusy, "workspace_busy");
                }
            });
        }

        private static void AgentNotePayloadUsesCanonicalIdentifiersAndUtcTimestamps()
        {
            var entry = new Entry
            {
                Id = new Guid("01234567-89AB-CDEF-0123-456789ABCDEF"),
                Title = "结构化 Note",
                Body = "正文",
                Category = "工作",
                IsFavorite = true,
                IsDeleted = true,
                CreatedUtc = new DateTime(2026, 8, 20, 1, 2, 3, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 8, 21, 4, 5, 6, DateTimeKind.Utc),
                DeletedUtc = new DateTime(2026, 8, 22, 7, 8, 9, DateTimeKind.Utc)
            };

            string json = AgentNotePayload.Serialize(entry);
            Require(json.Contains("\"schema\":\"seernote.note.v1\""), "Agent payload should identify its contract.");
            Require(json.Contains("\"id\":\"01234567-89ab-cdef-0123-456789abcdef\""), "Agent payload IDs should use canonical lowercase UUIDs.");
            Require(json.Contains("\"displayTitle\":\"结构化 Note\""), "Agent payload should include the human display title.");
            Require(json.Contains("\"createdUtc\":\"2026-08-20T01:02:03.0000000Z\"") && json.Contains("\"deletedUtc\":\"2026-08-22T07:08:09.0000000Z\""), "Agent payload timestamps should be ISO-8601 UTC.");
        }

        private static CliResult Run(string root, string[] args, string input = "")
        {
            var output = new StringWriter();
            var error = new StringWriter();
            int exitCode = CliApplication.Run(args, new StringReader(input ?? String.Empty), output, error, root);
            return new CliResult(exitCode, output.ToString(), error.ToString());
        }

        private static string ExtractId(string json)
        {
            Match match = Regex.Match(json ?? String.Empty, "\\\"id\\\":\\\"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\\\"");
            Require(match.Success, "CLI response should contain a canonical Note ID.");
            return match.Groups[1].Value;
        }

        private static void RequireSuccessJson(CliResult result, string command)
        {
            Require(result.ExitCode == CliExitCodes.Success, command + " should exit successfully. stderr: " + result.Error);
            Require(String.IsNullOrWhiteSpace(result.Error), command + " should not write to stderr on success.");
            Require(NonEmptyLineCount(result.Output) == 1, command + " should emit exactly one JSON object to stdout.");
            Require(result.Output.Contains("\"contract\":\"seernote.cli.v1\"") && result.Output.Contains("\"ok\":true") && result.Output.Contains("\"command\":\"" + command + "\""), command + " should use the success envelope.");
        }

        private static void RequireFailure(CliResult result, int exitCode, string errorCode)
        {
            Require(result.ExitCode == exitCode, errorCode + " should use exit code " + exitCode + ".");
            Require(String.IsNullOrWhiteSpace(result.Output), errorCode + " should not write to stdout.");
            Require(NonEmptyLineCount(result.Error) == 1, errorCode + " should emit exactly one JSON object to stderr.");
            Require(result.Error.Contains("\"contract\":\"seernote.cli.v1\"") && result.Error.Contains("\"ok\":false") && result.Error.Contains("\"code\":\"" + errorCode + "\""), errorCode + " should use the structured error envelope.");
        }

        private static int NonEmptyLineCount(string value)
        {
            return (value ?? String.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => !String.IsNullOrWhiteSpace(line));
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
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
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

        private sealed class CliResult
        {
            public CliResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public int ExitCode { get; private set; }
            public string Output { get; private set; }
            public string Error { get; private set; }
        }
    }
}
