using System;
using System.Collections.Generic;
using System.Diagnostics;
using SeerNote.Domain;

namespace SeerNote.Tests
{
    public static class DomainTests
    {
        public static void RunAll()
        {
            EntryCloneIsDeepAndStateValidates();
            UserSettingsDefaultCloneAndValidationCoverCloseBehavior();
            SearchFiltersSystemViewsAndChineseSubstrings();
            SearchSortsFavoritesAndTrashByExpectedDates();
            SearchThousandEntriesWithinBudget();
            PromptVariablesAreOrderedUniqueAndRenderOnce();
            PromptRenderingReportsMissingValues();
        }

        private static void UserSettingsDefaultCloneAndValidationCoverCloseBehavior()
        {
            var settings = new UserSettings();
            AssertEqual(CloseButtonBehavior.Exit, settings.CloseButtonBehavior, "Close button should default to a complete exit.");
            AssertEqual(AppTheme.Graphite, settings.Theme, "Existing installations should retain the graphite theme by default.");

            settings.CloseButtonBehavior = CloseButtonBehavior.MinimizeToTray;
            settings.Theme = AppTheme.Sage;
            UserSettings copy = settings.Clone();
            AssertEqual(CloseButtonBehavior.MinimizeToTray, copy.CloseButtonBehavior, "Cloned settings should preserve close button behavior.");
            AssertEqual(AppTheme.Sage, copy.Theme, "Cloned settings should preserve the selected theme.");

            copy.CloseButtonBehavior = (CloseButtonBehavior)99;
            string error;
            Assert(!copy.TryValidate(out error), "Unknown close button behavior must be rejected.");

            copy.CloseButtonBehavior = CloseButtonBehavior.Exit;
            copy.Theme = (AppTheme)99;
            Assert(!copy.TryValidate(out error), "Unknown application themes must be rejected.");
        }

        private static void SearchThousandEntriesWithinBudget()
        {
            var entries = new List<Entry>();
            for (var index = 0; index < 1000; index++)
            {
                entries.Add(NewEntry(
                    "中文标题 " + index,
                    index == 777 ? "需要命中的快速搜索内容" : "普通正文 " + index,
                    index % 2 == 0 ? "工作" : "生活",
                    index));
            }

            EntrySearch.Filter(entries, "不存在", SmartView.All);
            var stopwatch = Stopwatch.StartNew();
            IList<Entry> result = EntrySearch.Filter(entries, "快速搜索", SmartView.All);
            stopwatch.Stop();

            AssertEqual(1, result.Count, "Search should find the expected entry in 1,000 items.");
            Assert(stopwatch.ElapsedMilliseconds < 50, "Search over 1,000 entries exceeded the 50 ms budget: " + stopwatch.ElapsedMilliseconds + " ms.");
        }

        private static void EntryCloneIsDeepAndStateValidates()
        {
            var entry = NewEntry("原始标题", "第一行", "工作", 2);
            entry.Sticky.IsOpen = true;
            var state = new AppState();
            state.Categories.Add("工作");
            state.Entries.Add(entry);

            string error;
            Assert(state.TryValidate(out error), error);

            var copy = state.Clone();
            copy.Entries[0].Title = "修改后的副本";
            copy.Entries[0].Sticky.Width = 600;
            copy.Categories[0] = "副本分类";

            AssertEqual("原始标题", state.Entries[0].Title, "Clone must not share entry text.");
            AssertEqual(360d, state.Entries[0].Sticky.Width, "Clone must not share sticky state.");
            AssertEqual("工作", state.Categories[0], "Clone must not share the ordered category list.");

            copy.Entries.Add(entry.Clone());
            Assert(!copy.TryValidate(out error), "Duplicate entry IDs must be rejected.");
        }

        private static void SearchFiltersSystemViewsAndChineseSubstrings()
        {
            var memo = NewEntry("今日待办", "给客户发送报价", "工作", 1);
            var prompt = NewEntry("邮件模板", "请把{{客户}}的报价整理成摘要", "销售", 3);
            var favorite = NewEntry("收藏", "项目复盘", "工作", 2);
            favorite.IsFavorite = true;
            var deleted = NewEntry("已删除", "旧内容", "归档", 4);
            deleted.IsDeleted = true;
            deleted.DeletedUtc = deleted.UpdatedUtc;
            var entries = new[] { memo, prompt, favorite, deleted };

            var byBody = EntrySearch.Filter(entries, "客户发送", SmartView.All);
            AssertEqual(1, byBody.Count, "Chinese body substring should match.");
            AssertSame(memo, byBody[0], "Search must return the matching entry.");

            var byCategory = EntrySearch.Filter(entries, "销售", SmartView.All);
            AssertEqual(1, byCategory.Count, "Category substring should match across unified notes.");
            AssertSame(prompt, byCategory[0], "Unified note search should return template content without a separate type.");

            var favorites = EntrySearch.Filter(entries, null, SmartView.Favorite);
            AssertEqual(1, favorites.Count, "Favorite smart view should exclude non-favorites and trash.");
            AssertSame(favorite, favorites[0], "Favorite smart view returned the wrong entry.");

            var trash = EntrySearch.Filter(entries, String.Empty, SmartView.Trash);
            AssertEqual(1, trash.Count, "Trash smart view should include deleted entries only.");
            AssertSame(deleted, trash[0], "Trash smart view returned the wrong entry.");
        }

        private static void SearchSortsFavoritesAndTrashByExpectedDates()
        {
            var normal = NewEntry("普通", String.Empty, String.Empty, 3);
            var favorite = NewEntry("收藏", String.Empty, String.Empty, 1);
            favorite.IsFavorite = true;
            var oldDeleted = NewEntry("旧回收", String.Empty, String.Empty, 2);
            oldDeleted.IsDeleted = true;
            oldDeleted.DeletedUtc = oldDeleted.UpdatedUtc;
            var newDeleted = NewEntry("新回收", String.Empty, String.Empty, 4);
            newDeleted.IsDeleted = true;
            newDeleted.DeletedUtc = newDeleted.UpdatedUtc;
            var entries = new[] { normal, oldDeleted, favorite, newDeleted };

            var all = EntrySearch.Filter(entries, String.Empty, SmartView.All);
            AssertSame(favorite, all[0], "Favorites must sort before non-favorites.");
            AssertSame(normal, all[1], "Remaining entries must sort by most recent update.");

            var trash = EntrySearch.Filter(entries, String.Empty, SmartView.Trash);
            AssertSame(newDeleted, trash[0], "Trash must sort by deletion time descending.");
            AssertSame(oldDeleted, trash[1], "Trash sort order is incorrect.");
        }

        private static void PromptVariablesAreOrderedUniqueAndRenderOnce()
        {
            const string template = "给 {{ 客户 }} 写信，主题是 {{主题}}。{{客户}}，{{}} 保持原样。";
            var variables = PromptTemplate.Parse(template);

            AssertEqual(2, variables.Count, "Variables must be de-duplicated.");
            AssertEqual("客户", variables[0], "Variables must retain first-seen order.");
            AssertEqual("主题", variables[1], "Variables must retain first-seen order.");

            var result = PromptTemplate.Render(template, new Dictionary<string, string>
            {
                { "客户", "王女士" },
                { "主题", "本周报价" }
            });

            AssertEqual("给 王女士 写信，主题是 本周报价。王女士，{{}} 保持原样。", result, "Template rendering should replace every valid occurrence.");
        }

        private static void PromptRenderingReportsMissingValues()
        {
            string rendered;
            string error;
            var succeeded = PromptTemplate.TryRender("你好，{{姓名}}", new Dictionary<string, string>(), out rendered, out error);

            Assert(!succeeded, "Missing template values must fail validation.");
            Assert(rendered == null, "Failed rendering must not produce partial text.");
            Assert(error.IndexOf("姓名", StringComparison.Ordinal) >= 0, "The missing variable should be named in the error.");
        }

        private static Entry NewEntry(string title, string body, string category, int minutes)
        {
            var timestamp = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc).AddMinutes(minutes);
            return new Entry
            {
                Title = title,
                Body = body,
                Category = category,
                CreatedUtc = timestamp,
                UpdatedUtc = timestamp
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + "; actual: " + actual + ".");
            }
        }

        private static void AssertSame(object expected, object actual, string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
