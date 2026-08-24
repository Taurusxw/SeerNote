using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using SeerNote.Agent;
using SeerNote.Domain;
using SeerNote.Platform;
using SeerNote.Storage;

namespace SeerNote.Cli
{
    public static class CliExitCodes
    {
        public const int Success = 0;
        public const int InternalError = 1;
        public const int UsageError = 2;
        public const int NotFound = 3;
        public const int WorkspaceBusy = 4;
        public const int ValidationError = 5;
        public const int StorageError = 6;
    }

    public static class CliApplication
    {
        public const string ContractName = "seernote.cli.v1";

        private static readonly string[] DataCommands = { "categories", "list", "get", "create", "update", "delete", "restore" };

        public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error, string applicationRoot)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }
            if (String.IsNullOrWhiteSpace(applicationRoot))
            {
                throw new ArgumentException("Application root is required.", nameof(applicationRoot));
            }

            string command = null;
            try
            {
                CliRequest request = CliRequest.Parse(args);
                command = request.Command;
                if (command == "help")
                {
                    output.WriteLine(HelpText());
                    return CliExitCodes.Success;
                }
                if (command == "version")
                {
                    WriteSuccess(output, command, new CliData { Version = DisplayVersion() });
                    return CliExitCodes.Success;
                }
                if (command == "schema")
                {
                    WriteSuccess(output, command, new CliData { Schema = CreateSchema() });
                    return CliExitCodes.Success;
                }
                if (!DataCommands.Contains(command, StringComparer.Ordinal))
                {
                    throw CliFailure.Usage("unknown_command", "Unknown command: " + command + ". Run 'help' or 'schema'.");
                }

                string root = Path.GetFullPath(applicationRoot);
                string dataDirectory = Path.Combine(root, "data");
                Directory.CreateDirectory(dataDirectory);
                string lockPath = Path.Combine(dataDirectory, ".seernote.lock");
                string applicationId = "SeerNote.Cli|" + SingleInstanceGuard.GetDirectoryIdentity(root);
                SingleInstanceGuard guard;
                if (!SingleInstanceGuard.TryAcquire(applicationId, lockPath, out guard))
                {
                    throw new CliFailure(
                        CliExitCodes.WorkspaceBusy,
                        "workspace_busy",
                        "The SeerNote data directory is in use. Close the desktop app or retry after the other CLI command finishes.");
                }

                using (guard)
                {
                    var store = new PortableStore(root);
                    LoadResult load = store.Load();
                    if (!load.Success || load.State == null)
                    {
                        throw new CliFailure(CliExitCodes.StorageError, "load_failed", ErrorMessage(load.Error, "Unable to load SeerNote data."));
                    }
                    CliData data = Execute(request, input, load.State, store);
                    WriteSuccess(output, command, data);
                    return CliExitCodes.Success;
                }
            }
            catch (CliFailure failure)
            {
                WriteFailure(error, command, failure.Code, failure.Message);
                return failure.ExitCode;
            }
            catch (Exception exception)
            {
                WriteFailure(error, command, "internal_error", ErrorMessage(exception, "Unexpected CLI error."));
                return CliExitCodes.InternalError;
            }
        }

        private static CliData Execute(CliRequest request, TextReader input, AppState state, PortableStore store)
        {
            switch (request.Command)
            {
                case "categories":
                    request.RequireOnly();
                    return new CliData { Categories = new List<string>(state.Categories), Count = state.Categories.Count };
                case "list":
                    return List(request, state);
                case "get":
                    request.RequireOnly("id");
                    return new CliData { Note = AgentNotePayload.FromEntry(FindEntry(state, request.RequiredGuid("id"))) };
                case "create":
                    return Create(request, input, state, store);
                case "update":
                    return Update(request, input, state, store);
                case "delete":
                    return SoftDelete(request, state, store);
                case "restore":
                    return Restore(request, state, store);
                default:
                    throw CliFailure.Usage("unknown_command", "Unknown command: " + request.Command + ".");
            }
        }

        private static CliData List(CliRequest request, AppState state)
        {
            request.RequireOnly("query", "category", "view", "limit");
            SmartView view = ParseView(request.Optional("view"));
            IList<Entry> filtered = EntrySearch.Filter(state.Entries, request.Optional("query"), view);
            if (request.Has("category"))
            {
                string category = NormalizeCategory(request.Optional("category"));
                EnsureCategoryExists(state, category);
                string destination = category ?? String.Empty;
                filtered = filtered.Where(entry => String.Equals(entry.Category, destination, StringComparison.InvariantCultureIgnoreCase)).ToList();
            }

            int limit = request.OptionalInt("limit", 100, 1, 1000);
            var notes = filtered.Take(limit).Select(AgentNotePayload.FromEntry).ToList();
            return new CliData { Notes = notes, Count = notes.Count, Total = filtered.Count };
        }

        private static CliData Create(CliRequest request, TextReader input, AppState state, PortableStore store)
        {
            request.RequireOnly("title", "body", "body-file", "body-stdin", "category", "favorite");
            string title = request.Optional("title") ?? String.Empty;
            string body = ReadBody(request, input) ?? String.Empty;
            if (String.IsNullOrWhiteSpace(title) && String.IsNullOrWhiteSpace(body))
            {
                throw CliFailure.Validation("empty_note", "Create requires a non-empty --title or body source.");
            }
            string category = NormalizeCategory(request.Optional("category"));
            EnsureCategoryExists(state, category);
            bool favorite = request.OptionalBoolean("favorite", false);
            DateTime now = DateTime.UtcNow;
            var entry = new Entry
            {
                Title = title,
                Body = body,
                Category = category ?? String.Empty,
                IsFavorite = favorite,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            state.Entries.Add(entry);
            EntryOrder.MoveToGroupStart(state.Entries, entry);
            Save(store, state);
            return new CliData { Action = "created", Note = AgentNotePayload.FromEntry(entry) };
        }

        private static CliData Update(CliRequest request, TextReader input, AppState state, PortableStore store)
        {
            request.RequireOnly("id", "title", "body", "body-file", "body-stdin", "category", "favorite");
            Entry entry = FindEntry(state, request.RequiredGuid("id"));
            if (entry.IsDeleted)
            {
                throw CliFailure.Validation("note_deleted", "Deleted notes must be restored before they can be updated.");
            }

            bool changed = false;
            if (request.Has("title"))
            {
                entry.Title = request.Optional("title") ?? String.Empty;
                changed = true;
            }
            string body = ReadBody(request, input);
            if (body != null)
            {
                entry.Body = body;
                changed = true;
            }
            if (request.Has("category"))
            {
                string category = NormalizeCategory(request.Optional("category"));
                EnsureCategoryExists(state, category);
                entry.Category = category ?? String.Empty;
                changed = true;
            }
            if (request.Has("favorite"))
            {
                bool favorite = request.RequiredBoolean("favorite");
                bool favoriteChanged = favorite != entry.IsFavorite;
                entry.IsFavorite = favorite;
                if (favoriteChanged)
                {
                    EntryOrder.MoveToGroupStart(state.Entries, entry);
                }
                changed = true;
            }
            if (!changed)
            {
                throw CliFailure.Validation("no_changes", "Update requires at least one field option.");
            }

            entry.UpdatedUtc = DateTime.UtcNow;
            Save(store, state);
            return new CliData { Action = "updated", Note = AgentNotePayload.FromEntry(entry) };
        }

        private static CliData SoftDelete(CliRequest request, AppState state, PortableStore store)
        {
            request.RequireOnly("id");
            Entry entry = FindEntry(state, request.RequiredGuid("id"));
            if (entry.IsDeleted)
            {
                throw CliFailure.Validation("already_deleted", "The note is already in the recycle bin.");
            }
            DateTime now = DateTime.UtcNow;
            entry.IsDeleted = true;
            entry.DeletedUtc = now;
            entry.UpdatedUtc = now;
            if (entry.Sticky != null)
            {
                entry.Sticky.IsOpen = false;
            }
            EntryOrder.MoveToGroupStart(state.Entries, entry);
            Save(store, state);
            return new CliData { Action = "deleted", Note = AgentNotePayload.FromEntry(entry) };
        }

        private static CliData Restore(CliRequest request, AppState state, PortableStore store)
        {
            request.RequireOnly("id");
            Entry entry = FindEntry(state, request.RequiredGuid("id"));
            if (!entry.IsDeleted)
            {
                throw CliFailure.Validation("not_deleted", "The note is not in the recycle bin.");
            }
            entry.IsDeleted = false;
            entry.DeletedUtc = null;
            entry.UpdatedUtc = DateTime.UtcNow;
            EntryOrder.MoveToGroupStart(state.Entries, entry);
            Save(store, state);
            return new CliData { Action = "restored", Note = AgentNotePayload.FromEntry(entry) };
        }

        private static string ReadBody(CliRequest request, TextReader input)
        {
            int sources = (request.Has("body") ? 1 : 0) + (request.Has("body-file") ? 1 : 0) + (request.Has("body-stdin") ? 1 : 0);
            if (sources > 1)
            {
                throw CliFailure.Validation("multiple_body_sources", "Use only one of --body, --body-file, or --body-stdin.");
            }
            if (request.Has("body"))
            {
                return request.Optional("body") ?? String.Empty;
            }
            if (request.Has("body-stdin"))
            {
                return input.ReadToEnd();
            }
            if (request.Has("body-file"))
            {
                string path = request.Optional("body-file");
                try
                {
                    using (var reader = new StreamReader(Path.GetFullPath(path), new UTF8Encoding(false, true), true))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (Exception exception)
                {
                    throw CliFailure.Validation("body_file_error", "Unable to read --body-file: " + exception.GetBaseException().Message);
                }
            }
            return null;
        }

        private static void EnsureCategoryExists(AppState state, string category)
        {
            if (category == null)
            {
                return;
            }
            if (!state.Categories.Any(value => String.Equals(value, category, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw CliFailure.Validation("unknown_category", "Unknown category: " + category + ". Run 'categories' to inspect valid values.");
            }
        }

        private static string NormalizeCategory(string category)
        {
            return String.IsNullOrWhiteSpace(category) ? null : category.Trim();
        }

        private static Entry FindEntry(AppState state, Guid id)
        {
            Entry entry = state.Entries.FirstOrDefault(candidate => candidate != null && candidate.Id == id);
            if (entry == null)
            {
                throw new CliFailure(CliExitCodes.NotFound, "note_not_found", "No note exists with id " + id.ToString("D") + ".");
            }
            return entry;
        }

        private static SmartView ParseView(string value)
        {
            string normalized = (value ?? "all").Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "all": return SmartView.All;
                case "favorites": return SmartView.Favorite;
                case "trash": return SmartView.Trash;
                default: throw CliFailure.Validation("invalid_view", "--view must be all, favorites, or trash.");
            }
        }

        private static void Save(PortableStore store, AppState state)
        {
            SaveResult result = store.Save(state);
            if (!result.Success)
            {
                throw new CliFailure(CliExitCodes.StorageError, "save_failed", ErrorMessage(result.Error, "Unable to save SeerNote data."));
            }
            state.SavedUtc = result.SavedUtc;
        }

        private static void WriteSuccess(TextWriter output, string command, CliData data)
        {
            output.WriteLine(AgentJson.Serialize(new CliEnvelope { Ok = true, Command = command, Data = data }));
        }

        private static void WriteFailure(TextWriter error, string command, string code, string message)
        {
            error.WriteLine(AgentJson.Serialize(new CliEnvelope
            {
                Ok = false,
                Command = command,
                Error = new CliError { Code = code, Message = message }
            }));
        }

        private static CliSchema CreateSchema()
        {
            return new CliSchema
            {
                Contract = ContractName,
                NoteContract = AgentNotePayload.ContractName,
                Commands = new List<string> { "schema", "categories", "list", "get", "create", "update", "delete", "restore" },
                ExitCodes = new List<CliExitCode>
                {
                    new CliExitCode(0, "success"),
                    new CliExitCode(1, "internal_error"),
                    new CliExitCode(2, "usage_error"),
                    new CliExitCode(3, "not_found"),
                    new CliExitCode(4, "workspace_busy"),
                    new CliExitCode(5, "validation_error"),
                    new CliExitCode(6, "storage_error")
                },
                Mutations = new List<string> { "create", "update", "delete", "restore" },
                PermanentDeleteAvailable = false
            };
        }

        private static string HelpText()
        {
            return String.Join(Environment.NewLine, new[]
            {
                "SeerNote.Cli " + DisplayVersion(),
                "Agent-friendly local Note commands. Data commands emit one UTF-8 JSON object to stdout.",
                "",
                "Usage:",
                "  SeerNote.Cli.exe schema",
                "  SeerNote.Cli.exe categories",
                "  SeerNote.Cli.exe list [--query TEXT] [--category NAME] [--view all|favorites|trash] [--limit 1..1000]",
                "  SeerNote.Cli.exe get --id UUID",
                "  SeerNote.Cli.exe create [--title TEXT] [--body TEXT|--body-file PATH|--body-stdin] [--category NAME] [--favorite]",
                "  SeerNote.Cli.exe update --id UUID [--title TEXT] [--body TEXT|--body-file PATH|--body-stdin] [--category NAME] [--favorite true|false]",
                "  SeerNote.Cli.exe delete --id UUID",
                "  SeerNote.Cli.exe restore --id UUID",
                "",
                "Notes:",
                "  Close the SeerNote desktop app before data commands; both use the same single-writer lock.",
                "  delete moves a Note to the recycle bin. Permanent deletion is intentionally unavailable.",
                "  Options also accept --name=value. Run schema for the machine-readable contract."
            });
        }

        private static string DisplayVersion()
        {
            Version version = typeof(CliApplication).Assembly.GetName().Version;
            return version == null
                ? "0.0.0"
                : version.Major + "." + version.Minor + "." + version.Build;
        }

        private static string ErrorMessage(Exception error, string fallback)
        {
            return error == null ? fallback : error.GetBaseException().Message;
        }
    }

    internal sealed class CliRequest
    {
        private readonly Dictionary<string, string> _options;

        private CliRequest(string command, Dictionary<string, string> options)
        {
            Command = command;
            _options = options;
        }

        public string Command { get; private set; }

        public static CliRequest Parse(string[] args)
        {
            string[] values = args ?? new string[0];
            if (values.Length == 0)
            {
                return new CliRequest("help", new Dictionary<string, string>());
            }
            string first = values[0].Trim().ToLowerInvariant();
            if (first == "help" || first == "--help" || first == "-h")
            {
                return new CliRequest("help", new Dictionary<string, string>());
            }
            if (first == "version" || first == "--version")
            {
                return new CliRequest("version", new Dictionary<string, string>());
            }
            if (first.StartsWith("-", StringComparison.Ordinal))
            {
                throw CliFailure.Usage("missing_command", "The first argument must be a command. Run 'help' or 'schema'.");
            }

            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 1; index < values.Length; index++)
            {
                string token = values[index];
                if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length <= 2)
                {
                    throw CliFailure.Usage("unexpected_argument", "Unexpected positional argument: " + token + ".");
                }
                string option = token.Substring(2);
                string value = null;
                int equals = option.IndexOf('=');
                if (equals >= 0)
                {
                    value = option.Substring(equals + 1);
                    option = option.Substring(0, equals);
                }
                else if (index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = values[++index];
                }
                else if (option == "body-stdin" || option == "favorite")
                {
                    value = "true";
                }
                else
                {
                    throw CliFailure.Usage("missing_option_value", "Missing value for --" + option + ".");
                }

                if (String.IsNullOrWhiteSpace(option) || options.ContainsKey(option))
                {
                    throw CliFailure.Usage("duplicate_option", "Option --" + option + " may be supplied only once.");
                }
                options.Add(option, value);
            }
            return new CliRequest(first, options);
        }

        public bool Has(string name)
        {
            return _options.ContainsKey(name);
        }

        public string Optional(string name)
        {
            string value;
            return _options.TryGetValue(name, out value) ? value : null;
        }

        public Guid RequiredGuid(string name)
        {
            string value = Required(name);
            Guid parsed;
            if (!Guid.TryParseExact(value, "D", out parsed))
            {
                throw CliFailure.Validation("invalid_id", "--" + name + " must be a canonical UUID such as 01234567-89ab-cdef-0123-456789abcdef.");
            }
            return parsed;
        }

        public bool RequiredBoolean(string name)
        {
            string value = Required(name);
            bool parsed;
            if (!Boolean.TryParse(value, out parsed))
            {
                throw CliFailure.Validation("invalid_boolean", "--" + name + " must be true or false.");
            }
            return parsed;
        }

        public bool OptionalBoolean(string name, bool fallback)
        {
            return Has(name) ? RequiredBoolean(name) : fallback;
        }

        public int OptionalInt(string name, int fallback, int minimum, int maximum)
        {
            if (!Has(name))
            {
                return fallback;
            }
            int parsed;
            if (!Int32.TryParse(Required(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < minimum || parsed > maximum)
            {
                throw CliFailure.Validation("invalid_integer", "--" + name + " must be between " + minimum + " and " + maximum + ".");
            }
            return parsed;
        }

        public void RequireOnly(params string[] allowed)
        {
            var set = new HashSet<string>(allowed ?? new string[0], StringComparer.Ordinal);
            string unknown = _options.Keys.FirstOrDefault(key => !set.Contains(key));
            if (unknown != null)
            {
                throw CliFailure.Usage("unknown_option", "Unknown option for " + Command + ": --" + unknown + ".");
            }
        }

        private string Required(string name)
        {
            string value;
            if (!_options.TryGetValue(name, out value) || String.IsNullOrWhiteSpace(value))
            {
                throw CliFailure.Usage("missing_required_option", "Missing required option --" + name + ".");
            }
            return value;
        }
    }

    internal sealed class CliFailure : Exception
    {
        public CliFailure(int exitCode, string code, string message) : base(message)
        {
            ExitCode = exitCode;
            Code = code;
        }

        public int ExitCode { get; private set; }
        public string Code { get; private set; }

        public static CliFailure Usage(string code, string message)
        {
            return new CliFailure(CliExitCodes.UsageError, code, message);
        }

        public static CliFailure Validation(string code, string message)
        {
            return new CliFailure(CliExitCodes.ValidationError, code, message);
        }
    }

    [DataContract]
    internal sealed class CliEnvelope
    {
        [DataMember(Name = "contract", Order = 1)] public string Contract { get; set; } = CliApplication.ContractName;
        [DataMember(Name = "ok", Order = 2)] public bool Ok { get; set; }
        [DataMember(Name = "command", Order = 3, EmitDefaultValue = false)] public string Command { get; set; }
        [DataMember(Name = "data", Order = 4, EmitDefaultValue = false)] public CliData Data { get; set; }
        [DataMember(Name = "error", Order = 5, EmitDefaultValue = false)] public CliError Error { get; set; }
    }

    [DataContract]
    internal sealed class CliData
    {
        [DataMember(Name = "action", Order = 1, EmitDefaultValue = false)] public string Action { get; set; }
        [DataMember(Name = "note", Order = 2, EmitDefaultValue = false)] public AgentNotePayload Note { get; set; }
        [DataMember(Name = "notes", Order = 3, EmitDefaultValue = false)] public List<AgentNotePayload> Notes { get; set; }
        [DataMember(Name = "categories", Order = 4, EmitDefaultValue = false)] public List<string> Categories { get; set; }
        [DataMember(Name = "count", Order = 5, EmitDefaultValue = false)] public int? Count { get; set; }
        [DataMember(Name = "total", Order = 6, EmitDefaultValue = false)] public int? Total { get; set; }
        [DataMember(Name = "version", Order = 7, EmitDefaultValue = false)] public string Version { get; set; }
        [DataMember(Name = "schema", Order = 8, EmitDefaultValue = false)] public CliSchema Schema { get; set; }
    }

    [DataContract]
    internal sealed class CliError
    {
        [DataMember(Name = "code", Order = 1)] public string Code { get; set; }
        [DataMember(Name = "message", Order = 2)] public string Message { get; set; }
    }

    [DataContract]
    internal sealed class CliSchema
    {
        [DataMember(Name = "contract", Order = 1)] public string Contract { get; set; }
        [DataMember(Name = "noteContract", Order = 2)] public string NoteContract { get; set; }
        [DataMember(Name = "commands", Order = 3)] public List<string> Commands { get; set; }
        [DataMember(Name = "mutations", Order = 4)] public List<string> Mutations { get; set; }
        [DataMember(Name = "exitCodes", Order = 5)] public List<CliExitCode> ExitCodes { get; set; }
        [DataMember(Name = "permanentDeleteAvailable", Order = 6)] public bool PermanentDeleteAvailable { get; set; }
    }

    [DataContract]
    internal sealed class CliExitCode
    {
        public CliExitCode(int value, string meaning)
        {
            Value = value;
            Meaning = meaning;
        }

        [DataMember(Name = "value", Order = 1)] public int Value { get; set; }
        [DataMember(Name = "meaning", Order = 2)] public string Meaning { get; set; }
    }
}
