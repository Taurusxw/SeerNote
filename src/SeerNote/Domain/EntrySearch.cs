using System;
using System.Collections.Generic;

namespace SeerNote.Domain
{
    public static class EntrySearch
    {
        public static IList<Entry> Search(IEnumerable<Entry> entries, string query, SmartView smartView)
        {
            return Filter(entries, query, smartView);
        }

        public static IList<Entry> Filter(IEnumerable<Entry> entries, string query, SmartView smartView)
        {
            if (entries == null)
            {
                throw new ArgumentNullException("entries");
            }

            if (!Enum.IsDefined(typeof(SmartView), smartView))
            {
                throw new ArgumentOutOfRangeException("smartView");
            }

            var result = new List<Entry>();
            var favorites = smartView == SmartView.All ? new List<Entry>() : null;
            var normalizedQuery = (query ?? String.Empty).Trim();

            foreach (var entry in entries)
            {
                if (entry != null && IsInSmartView(entry, smartView) && Matches(entry, normalizedQuery))
                {
                    if (favorites != null && entry.IsFavorite)
                    {
                        favorites.Add(entry);
                    }
                    else
                    {
                        result.Add(entry);
                    }
                }
            }

            if (favorites != null)
            {
                favorites.AddRange(result);
                return favorites;
            }
            return result;
        }

        public static bool Matches(Entry entry, string query)
        {
            if (entry == null)
            {
                return false;
            }

            var normalizedQuery = (query ?? String.Empty).Trim();
            if (normalizedQuery.Length == 0)
            {
                return true;
            }

            return Contains(entry.Title, normalizedQuery) ||
                   Contains(entry.Body, normalizedQuery) ||
                   Contains(entry.Category, normalizedQuery);
        }

        private static bool IsInSmartView(Entry entry, SmartView smartView)
        {
            switch (smartView)
            {
                case SmartView.All:
                    return !entry.IsDeleted;
                case SmartView.Favorite:
                    return !entry.IsDeleted && entry.IsFavorite;
                case SmartView.Trash:
                    return entry.IsDeleted;
                default:
                    return false;
            }
        }

        private static bool Contains(string text, string query)
        {
            return !String.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

    }
}
