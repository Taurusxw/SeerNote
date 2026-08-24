using System;
using System.Collections.Generic;

namespace SeerNote.Domain
{
    public sealed class AppState
    {
        public const int CurrentSchemaVersion = 3;

        public AppState()
        {
            SchemaVersion = CurrentSchemaVersion;
            SavedUtc = DateTime.UtcNow;
            Settings = new UserSettings();
            Categories = new List<string>();
            Entries = new List<Entry>();
        }

        public int SchemaVersion { get; set; }

        public DateTime SavedUtc { get; set; }

        public UserSettings Settings { get; set; }

        public List<string> Categories { get; set; }

        public List<Entry> Entries { get; set; }

        public AppState Clone()
        {
            var clone = new AppState
            {
                SchemaVersion = SchemaVersion,
                SavedUtc = SavedUtc,
                Settings = Settings == null ? null : Settings.Clone(),
                Categories = Categories == null ? null : new List<string>(Categories),
                Entries = new List<Entry>()
            };

            if (Entries != null)
            {
                foreach (var entry in Entries)
                {
                    clone.Entries.Add(entry == null ? null : entry.Clone());
                }
            }

            return clone;
        }

        public bool TryValidate(out string error)
        {
            if (SchemaVersion != CurrentSchemaVersion)
            {
                error = "Unsupported schema version.";
                return false;
            }

            if (SavedUtc == DateTime.MinValue)
            {
                error = "Saved timestamp is required.";
                return false;
            }

            if (Settings == null)
            {
                error = "Settings are required.";
                return false;
            }

            if (!Settings.TryValidate(out error))
            {
                return false;
            }

            if (Entries == null)
            {
                error = "Entries are required.";
                return false;
            }

            if (Categories == null)
            {
                error = "Categories are required.";
                return false;
            }

            var categoryNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (string category in Categories)
            {
                if (String.IsNullOrWhiteSpace(category) || !String.Equals(category, category.Trim(), StringComparison.Ordinal))
                {
                    error = "Category names must be non-empty and trimmed.";
                    return false;
                }
                if (!categoryNames.Add(category))
                {
                    error = "Category names must be unique.";
                    return false;
                }
            }

            var ids = new HashSet<Guid>();
            foreach (var entry in Entries)
            {
                if (entry == null)
                {
                    error = "Entries cannot contain null values.";
                    return false;
                }

                if (!entry.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(entry.Id))
                {
                    error = "Entry IDs must be unique.";
                    return false;
                }

                if (!String.IsNullOrWhiteSpace(entry.Category)
                    && (!String.Equals(entry.Category, entry.Category.Trim(), StringComparison.Ordinal)
                        || !categoryNames.Contains(entry.Category)))
                {
                    error = "Every entry category must exist in the ordered category list.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public void Validate()
        {
            string error;
            if (!TryValidate(out error))
            {
                throw new InvalidOperationException(error);
            }
        }
    }
}
