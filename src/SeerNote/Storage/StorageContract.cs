using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using SeerNote.Domain;

namespace SeerNote.Storage
{
    // The persisted contract deliberately has no dependency on Domain member
    // names: JSON remains lowercase and timestamps remain portable ISO-8601.
    [DataContract]
    internal sealed class StoredState
    {
        [DataMember(Name = "schemaVersion", Order = 1)] public int SchemaVersion { get; set; }
        [DataMember(Name = "savedUtc", Order = 2)] public string SavedUtc { get; set; }
        [DataMember(Name = "settings", Order = 3)] public StoredSettings Settings { get; set; }
        [DataMember(Name = "categories", Order = 4, EmitDefaultValue = false)] public List<string> Categories { get; set; }
        [DataMember(Name = "entries", Order = 5)] public List<StoredEntry> Entries { get; set; }

        public static StoredState FromDomain(AppState state)
        {
            var result = new StoredState
            {
                SchemaVersion = state.SchemaVersion,
                SavedUtc = FormatUtc(state.SavedUtc),
                Settings = StoredSettings.FromDomain(state.Settings),
                Categories = new List<string>(state.Categories),
                Entries = new List<StoredEntry>()
            };
            foreach (var entry in state.Entries)
            {
                result.Entries.Add(StoredEntry.FromDomain(entry));
            }
            return result;
        }

        public AppState ToDomain()
        {
            if (Entries == null)
            {
                throw new SerializationException("entries is required.");
            }
            var entries = new List<Entry>();
            foreach (var entry in Entries)
            {
                entries.Add(entry == null ? null : entry.ToDomain());
            }
            var categories = SchemaVersion == 1
                ? CategoriesFromLegacyEntries(entries)
                : Categories == null ? null : new List<string>(Categories);
            return new AppState
            {
                SchemaVersion = SchemaVersion == 1 ? AppState.CurrentSchemaVersion : SchemaVersion,
                SavedUtc = ParseUtc(SavedUtc, "savedUtc"),
                Settings = Settings == null ? null : Settings.ToDomain(),
                Categories = categories,
                Entries = entries
            };
        }

        private static List<string> CategoriesFromLegacyEntries(IEnumerable<Entry> entries)
        {
            var categories = new List<string>();
            var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (Entry entry in entries)
            {
                string category = entry == null || String.IsNullOrWhiteSpace(entry.Category) ? null : entry.Category.Trim();
                if (category != null && seen.Add(category))
                {
                    categories.Add(category);
                }
                if (entry != null)
                {
                    entry.Category = category ?? String.Empty;
                }
            }
            return categories;
        }

        internal static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        internal static DateTime ParseUtc(string value, string fieldName)
        {
            DateTime parsed;
            if (String.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            {
                throw new SerializationException(fieldName + " is not a valid ISO-8601 UTC timestamp.");
            }
            return parsed.ToUniversalTime();
        }
    }

    [DataContract]
    internal sealed class StoredSettings
    {
        [DataMember(Name = "globalHotkey", Order = 1)] public string GlobalHotkey { get; set; }
        [DataMember(Name = "windowBounds", Order = 2)] public StoredBounds WindowBounds { get; set; }
        [DataMember(Name = "lastSmartView", Order = 3)] public string LastSmartView { get; set; }
        [DataMember(Name = "closeButtonBehavior", Order = 4, EmitDefaultValue = false)] public string CloseButtonBehavior { get; set; }
        [DataMember(Name = "theme", Order = 5, EmitDefaultValue = false)] public string Theme { get; set; }

        public static StoredSettings FromDomain(UserSettings settings)
        {
            return new StoredSettings
            {
                GlobalHotkey = settings.GlobalHotkey,
                WindowBounds = StoredBounds.FromDomain(settings.WindowBounds),
                LastSmartView = settings.LastSmartView.ToString().ToLowerInvariant(),
                CloseButtonBehavior = settings.CloseButtonBehavior.ToString().ToLowerInvariant(),
                Theme = settings.Theme.ToString().ToLowerInvariant()
            };
        }

        public UserSettings ToDomain()
        {
            SmartView view;
            if (String.Equals(LastSmartView, "memo", StringComparison.OrdinalIgnoreCase)
                || String.Equals(LastSmartView, "prompt", StringComparison.OrdinalIgnoreCase))
            {
                view = SmartView.All;
            }
            else if (!Enum.TryParse(LastSmartView, true, out view))
            {
                throw new SerializationException("lastSmartView is invalid.");
            }

            SeerNote.Domain.CloseButtonBehavior closeButtonBehavior;
            if (String.IsNullOrWhiteSpace(CloseButtonBehavior))
            {
                closeButtonBehavior = SeerNote.Domain.CloseButtonBehavior.Exit;
            }
            else if (!Enum.TryParse(CloseButtonBehavior, true, out closeButtonBehavior))
            {
                throw new SerializationException("closeButtonBehavior is invalid.");
            }

            AppTheme theme;
            if (String.IsNullOrWhiteSpace(Theme))
            {
                theme = AppTheme.Graphite;
            }
            else if (!Enum.TryParse(Theme, true, out theme))
            {
                throw new SerializationException("theme is invalid.");
            }

            return new UserSettings
            {
                GlobalHotkey = GlobalHotkey,
                WindowBounds = WindowBounds == null ? null : WindowBounds.ToDomain(),
                LastSmartView = view,
                CloseButtonBehavior = closeButtonBehavior,
                Theme = theme
            };
        }
    }

    [DataContract]
    internal sealed class StoredBounds
    {
        [DataMember(Name = "left", Order = 1)] public double Left { get; set; }
        [DataMember(Name = "top", Order = 2)] public double Top { get; set; }
        [DataMember(Name = "width", Order = 3)] public double Width { get; set; }
        [DataMember(Name = "height", Order = 4)] public double Height { get; set; }

        public static StoredBounds FromDomain(WindowBounds bounds)
        {
            return new StoredBounds { Left = bounds.Left, Top = bounds.Top, Width = bounds.Width, Height = bounds.Height };
        }
        public WindowBounds ToDomain()
        {
            return new WindowBounds { Left = Left, Top = Top, Width = Width, Height = Height };
        }
    }

    [DataContract]
    internal sealed class StoredSticky
    {
        [DataMember(Name = "isOpen", Order = 1)] public bool IsOpen { get; set; }
        [DataMember(Name = "left", Order = 2)] public double Left { get; set; }
        [DataMember(Name = "top", Order = 3)] public double Top { get; set; }
        [DataMember(Name = "width", Order = 4)] public double Width { get; set; }
        [DataMember(Name = "height", Order = 5)] public double Height { get; set; }

        public static StoredSticky FromDomain(StickyState sticky)
        {
            return new StoredSticky { IsOpen = sticky.IsOpen, Left = sticky.Left, Top = sticky.Top, Width = sticky.Width, Height = sticky.Height };
        }
        public StickyState ToDomain()
        {
            return new StickyState { IsOpen = IsOpen, Left = Left, Top = Top, Width = Width, Height = Height };
        }
    }

    [DataContract]
    internal sealed class StoredEntry
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "type", Order = 2, EmitDefaultValue = false)] public string Type { get; set; }
        [DataMember(Name = "title", Order = 3)] public string Title { get; set; }
        [DataMember(Name = "body", Order = 4)] public string Body { get; set; }
        [DataMember(Name = "category", Order = 5)] public string Category { get; set; }
        [DataMember(Name = "isFavorite", Order = 6)] public bool IsFavorite { get; set; }
        [DataMember(Name = "isDeleted", Order = 7)] public bool IsDeleted { get; set; }
        [DataMember(Name = "sticky", Order = 8)] public StoredSticky Sticky { get; set; }
        [DataMember(Name = "createdUtc", Order = 9)] public string CreatedUtc { get; set; }
        [DataMember(Name = "updatedUtc", Order = 10)] public string UpdatedUtc { get; set; }
        [DataMember(Name = "deletedUtc", Order = 11)] public string DeletedUtc { get; set; }

        public static StoredEntry FromDomain(Entry entry)
        {
            return new StoredEntry
            {
                Id = entry.Id.ToString("D"), Title = entry.Title, Body = entry.Body,
                Category = entry.Category, IsFavorite = entry.IsFavorite, IsDeleted = entry.IsDeleted, Sticky = StoredSticky.FromDomain(entry.Sticky),
                CreatedUtc = StoredState.FormatUtc(entry.CreatedUtc), UpdatedUtc = StoredState.FormatUtc(entry.UpdatedUtc),
                DeletedUtc = entry.DeletedUtc.HasValue ? StoredState.FormatUtc(entry.DeletedUtc.Value) : null
            };
        }

        public Entry ToDomain()
        {
            Guid id;
            if (!Guid.TryParse(Id, out id))
            {
                throw new SerializationException("Entry id is invalid.");
            }
            return new Entry
            {
                Id = id, Title = Title, Body = Body, Category = Category, IsFavorite = IsFavorite, IsDeleted = IsDeleted,
                Sticky = Sticky == null ? null : Sticky.ToDomain(), CreatedUtc = StoredState.ParseUtc(CreatedUtc, "createdUtc"),
                UpdatedUtc = StoredState.ParseUtc(UpdatedUtc, "updatedUtc"),
                DeletedUtc = String.IsNullOrWhiteSpace(DeletedUtc) ? (DateTime?)null : StoredState.ParseUtc(DeletedUtc, "deletedUtc")
            };
        }
    }
}
