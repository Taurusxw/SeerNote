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
            var normalizedQuery = (query ?? String.Empty).Trim();

            foreach (var entry in entries)
            {
                if (entry != null && IsInSmartView(entry, smartView) && Matches(entry, normalizedQuery))
                {
                    result.Add(entry);
                }
            }

            result.Sort(new EntryComparer(smartView == SmartView.Trash));
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

        private sealed class EntryComparer : IComparer<Entry>
        {
            private readonly bool _isTrash;

            public EntryComparer(bool isTrash)
            {
                _isTrash = isTrash;
            }

            public int Compare(Entry x, Entry y)
            {
                if (_isTrash)
                {
                    var deletedComparison = Nullable.Compare(y.DeletedUtc, x.DeletedUtc);
                    if (deletedComparison != 0)
                    {
                        return deletedComparison;
                    }
                }
                else
                {
                    var favoriteComparison = y.IsFavorite.CompareTo(x.IsFavorite);
                    if (favoriteComparison != 0)
                    {
                        return favoriteComparison;
                    }
                }

                var updatedComparison = y.UpdatedUtc.CompareTo(x.UpdatedUtc);
                if (updatedComparison != 0)
                {
                    return updatedComparison;
                }

                return x.Id.CompareTo(y.Id);
            }
        }
    }
}
