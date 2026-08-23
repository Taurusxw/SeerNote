using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SeerNote.Domain;

namespace SeerNote.Presentation
{
    public sealed class NavigationSnapshot
    {
        private NavigationSnapshot(int allCount, int favoriteCount, int trashCount, IList<string> categories, IDictionary<string, int> categoryCounts)
        {
            AllCount = allCount;
            FavoriteCount = favoriteCount;
            TrashCount = trashCount;
            Categories = new ReadOnlyCollection<string>(categories);
            CategoryCounts = new ReadOnlyDictionary<string, int>(categoryCounts);
        }

        public int AllCount { get; private set; }

        public int FavoriteCount { get; private set; }

        public int TrashCount { get; private set; }

        public IList<string> Categories { get; private set; }

        public IDictionary<string, int> CategoryCounts { get; private set; }

        public static NavigationSnapshot Create(IEnumerable<Entry> entries, IEnumerable<string> categories)
        {
            if (entries == null)
            {
                throw new ArgumentNullException("entries");
            }
            if (categories == null)
            {
                throw new ArgumentNullException("categories");
            }

            int allCount = 0;
            int favoriteCount = 0;
            int trashCount = 0;
            var categoryCounts = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            foreach (Entry entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }
                if (entry.IsDeleted)
                {
                    trashCount++;
                    continue;
                }

                allCount++;
                if (entry.IsFavorite)
                {
                    favoriteCount++;
                }
                if (String.IsNullOrWhiteSpace(entry.Category))
                {
                    continue;
                }

                string category = entry.Category.Trim();
                int count;
                categoryCounts.TryGetValue(category, out count);
                categoryCounts[category] = count + 1;
            }

            return new NavigationSnapshot(allCount, favoriteCount, trashCount, new List<string>(categories), categoryCounts);
        }

        public bool HasSameContent(NavigationSnapshot other)
        {
            if (other == null || AllCount != other.AllCount || FavoriteCount != other.FavoriteCount || TrashCount != other.TrashCount || CategoryCounts.Count != other.CategoryCounts.Count || !HasSameCategoryOrder(other))
            {
                return false;
            }
            foreach (KeyValuePair<string, int> pair in CategoryCounts)
            {
                int otherCount;
                if (!other.CategoryCounts.TryGetValue(pair.Key, out otherCount) || pair.Value != otherCount)
                {
                    return false;
                }
            }
            return true;
        }

        public bool HasSameCategoryOrder(NavigationSnapshot other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other == null || Categories.Count != other.Categories.Count)
            {
                return false;
            }
            for (int index = 0; index < Categories.Count; index++)
            {
                if (!String.Equals(Categories[index], other.Categories[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
