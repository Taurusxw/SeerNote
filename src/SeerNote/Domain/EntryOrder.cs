using System;
using System.Collections.Generic;

namespace SeerNote.Domain
{
    public static class EntryOrder
    {
        public static void ApplyLegacyOrder(List<Entry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
            entries.Sort(LegacyEntryOrderComparer.Instance);
        }

        public static void MoveToGroupStart(IList<Entry> entries, Entry entry)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            int sourceIndex = IndexOf(entries, entry.Id);
            if (sourceIndex >= 0)
            {
                entries.RemoveAt(sourceIndex);
            }

            int insertIndex = 0;
            while (insertIndex < entries.Count && !IsSameGroup(entry, entries[insertIndex]))
            {
                insertIndex++;
            }
            entries.Insert(insertIndex < entries.Count ? insertIndex : 0, entry);
        }

        public static bool ReorderVisible(
            IList<Entry> entries,
            IList<Entry> visibleEntries,
            Guid sourceId,
            Guid targetId,
            bool insertAfter)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
            if (visibleEntries == null)
            {
                throw new ArgumentNullException(nameof(visibleEntries));
            }
            if (sourceId == Guid.Empty || targetId == Guid.Empty || sourceId == targetId)
            {
                return false;
            }

            Entry source = Find(visibleEntries, sourceId);
            Entry target = Find(visibleEntries, targetId);
            if (source == null || target == null || !IsSameGroup(source, target))
            {
                return false;
            }

            var reordered = new List<Entry>();
            for (int index = 0; index < visibleEntries.Count; index++)
            {
                Entry candidate = visibleEntries[index];
                if (candidate != null && IsSameGroup(source, candidate))
                {
                    reordered.Add(candidate);
                }
            }
            if (reordered.Count < 2 || !reordered.Remove(source))
            {
                return false;
            }

            int targetIndex = reordered.IndexOf(target);
            if (targetIndex < 0)
            {
                return false;
            }
            reordered.Insert(insertAfter ? targetIndex + 1 : targetIndex, source);

            var visibleIds = new HashSet<Guid>();
            for (int index = 0; index < reordered.Count; index++)
            {
                visibleIds.Add(reordered[index].Id);
            }

            var slots = new List<int>();
            for (int index = 0; index < entries.Count; index++)
            {
                Entry candidate = entries[index];
                if (candidate != null && visibleIds.Contains(candidate.Id))
                {
                    slots.Add(index);
                }
            }
            if (slots.Count != reordered.Count)
            {
                return false;
            }

            bool changed = false;
            for (int index = 0; index < slots.Count; index++)
            {
                if (!ReferenceEquals(entries[slots[index]], reordered[index]))
                {
                    changed = true;
                    break;
                }
            }
            if (!changed)
            {
                return false;
            }

            for (int index = 0; index < slots.Count; index++)
            {
                entries[slots[index]] = reordered[index];
            }
            return true;
        }

        public static bool IsSameGroup(Entry left, Entry right)
        {
            if (left == null || right == null || left.IsDeleted != right.IsDeleted)
            {
                return false;
            }
            return left.IsDeleted || left.IsFavorite == right.IsFavorite;
        }

        private static int IndexOf(IList<Entry> entries, Guid id)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                Entry candidate = entries[index];
                if (candidate != null && candidate.Id == id)
                {
                    return index;
                }
            }
            return -1;
        }

        private static Entry Find(IList<Entry> entries, Guid id)
        {
            int index = IndexOf(entries, id);
            return index < 0 ? null : entries[index];
        }

        private sealed class LegacyEntryOrderComparer : IComparer<Entry>
        {
            public static readonly LegacyEntryOrderComparer Instance = new LegacyEntryOrderComparer();

            public int Compare(Entry left, Entry right)
            {
                if (left == null || right == null)
                {
                    return left == null ? (right == null ? 0 : 1) : -1;
                }

                int deletedComparison = left.IsDeleted.CompareTo(right.IsDeleted);
                if (deletedComparison != 0)
                {
                    return deletedComparison;
                }
                if (left.IsDeleted)
                {
                    int deletionTimeComparison = Nullable.Compare(right.DeletedUtc, left.DeletedUtc);
                    if (deletionTimeComparison != 0)
                    {
                        return deletionTimeComparison;
                    }
                }
                else
                {
                    int favoriteComparison = right.IsFavorite.CompareTo(left.IsFavorite);
                    if (favoriteComparison != 0)
                    {
                        return favoriteComparison;
                    }
                }

                int updatedComparison = right.UpdatedUtc.CompareTo(left.UpdatedUtc);
                return updatedComparison != 0 ? updatedComparison : left.Id.CompareTo(right.Id);
            }
        }
    }
}
