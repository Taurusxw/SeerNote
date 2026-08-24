using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SeerNote.Domain;

namespace SeerNote.Presentation
{
    internal sealed class EntryContextMenu : ContextMenu
    {
        private readonly bool _deletedMenu;
        private readonly Action<Entry> _copyBody;
        private readonly Action<Entry> _copyId;
        private readonly Action<Entry> _copyJson;
        private readonly Action<Entry> _toggleFavorite;
        private readonly Action<Entry> _openSticky;
        private readonly Action<Entry> _softDelete;
        private readonly Action<Entry> _restore;
        private readonly Action<Entry> _permanentDelete;
        private readonly Action<Entry, string> _moveEntry;
        private readonly Func<IList<string>> _categoriesProvider;
        private readonly RoutedEventHandler _moveCategoryHandler;
        private IList<string> _categorySnapshot;
        private Entry _entry;
        private MenuItem _favorite;
        private MenuItem _move;
        private MenuItem _uncategorized;
        private MenuItem _checkedCategory;

        public EntryContextMenu(
            bool deletedMenu,
            Func<IList<string>> categoriesProvider,
            Action<Entry> copyBody,
            Action<Entry> copyId,
            Action<Entry> copyJson,
            Action<Entry> toggleFavorite,
            Action<Entry> openSticky,
            Action<Entry> softDelete,
            Action<Entry> restore,
            Action<Entry> permanentDelete,
            Action<Entry, string> moveEntry)
        {
            SetResourceReference(StyleProperty, typeof(ContextMenu));
            _deletedMenu = deletedMenu;
            _copyBody = copyBody ?? throw new ArgumentNullException(nameof(copyBody));
            _copyId = copyId ?? throw new ArgumentNullException(nameof(copyId));
            _copyJson = copyJson ?? throw new ArgumentNullException(nameof(copyJson));
            _toggleFavorite = toggleFavorite ?? throw new ArgumentNullException(nameof(toggleFavorite));
            _openSticky = openSticky ?? throw new ArgumentNullException(nameof(openSticky));
            _softDelete = softDelete ?? throw new ArgumentNullException(nameof(softDelete));
            _restore = restore ?? throw new ArgumentNullException(nameof(restore));
            _permanentDelete = permanentDelete ?? throw new ArgumentNullException(nameof(permanentDelete));
            _moveEntry = moveEntry ?? throw new ArgumentNullException(nameof(moveEntry));
            _categoriesProvider = categoriesProvider ?? throw new ArgumentNullException(nameof(categoriesProvider));
            _moveCategoryHandler = MoveCategoryOnClick;
            Closed += EntryContextMenuOnClosed;

            if (_deletedMenu)
            {
                BuildDeletedMenu();
            }
            else
            {
                BuildActiveMenu(_categoriesProvider());
            }
        }

        public void Prepare(Entry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }
            if (entry.IsDeleted != _deletedMenu)
            {
                throw new InvalidOperationException("The entry does not match this context-menu mode.");
            }

            _entry = entry;
            if (_deletedMenu)
            {
                return;
            }

            EnsureCategories(_categoriesProvider());
            _favorite.Header = entry.IsFavorite ? "取消收藏置顶" : "收藏置顶";
            UpdateCategoryCheck(entry.Category);
        }

        private void BuildActiveMenu(IList<string> categories)
        {
            var copy = new MenuItem { Header = "复制正文" };
            copy.Click += CopyBodyOnClick;
            Items.Add(copy);
            AddAgentCopyItems();
            Items.Add(new Separator());

            _favorite = new MenuItem { Header = "收藏置顶" };
            _favorite.Click += ToggleFavoriteOnClick;
            Items.Add(_favorite);

            var sticky = new MenuItem { Header = "打开置顶小窗" };
            sticky.Click += OpenStickyOnClick;
            Items.Add(sticky);

            _move = new MenuItem { Header = "移动到分类" };
            _uncategorized = new MenuItem { Header = "未分类", IsCheckable = true };
            _uncategorized.Click += _moveCategoryHandler;
            Items.Add(_move);
            EnsureCategories(categories);

            Items.Add(new Separator());
            var delete = new MenuItem { Header = "移到回收站" };
            delete.Click += SoftDeleteOnClick;
            Items.Add(delete);
        }

        private void BuildDeletedMenu()
        {
            AddAgentCopyItems();
            Items.Add(new Separator());

            var restore = new MenuItem { Header = "还原 Note" };
            restore.Click += RestoreOnClick;
            Items.Add(restore);

            Items.Add(new Separator());
            var permanentDelete = new MenuItem { Header = "永久删除" };
            permanentDelete.Click += PermanentDeleteOnClick;
            Items.Add(permanentDelete);
        }

        private void AddAgentCopyItems()
        {
            var copyId = new MenuItem { Header = "复制 Note ID" };
            copyId.Click += CopyIdOnClick;
            Items.Add(copyId);

            var copyJson = new MenuItem { Header = "复制为 JSON" };
            copyJson.Click += CopyJsonOnClick;
            Items.Add(copyJson);
        }

        private void EnsureCategories(IList<string> categories)
        {
            if (ReferenceEquals(_categorySnapshot, categories))
            {
                return;
            }
            if (HasSameCategoryOrder(categories))
            {
                _categorySnapshot = categories;
                return;
            }

            _move.Items.Clear();
            _move.Items.Add(_uncategorized);
            for (int index = 0; index < categories.Count; index++)
            {
                string category = categories[index];
                var item = new MenuItem
                {
                    Header = category,
                    Tag = category,
                    IsCheckable = true
                };
                item.Click += _moveCategoryHandler;
                _move.Items.Add(item);
            }
            _categorySnapshot = categories;
            _checkedCategory = null;
        }

        private bool HasSameCategoryOrder(IList<string> categories)
        {
            if (_categorySnapshot == null || _categorySnapshot.Count != categories.Count)
            {
                return false;
            }
            for (int index = 0; index < categories.Count; index++)
            {
                if (!String.Equals(_categorySnapshot[index], categories[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private void UpdateCategoryCheck(string category)
        {
            _uncategorized.IsChecked = String.IsNullOrWhiteSpace(category);
            if (_checkedCategory != null)
            {
                _checkedCategory.IsChecked = false;
                _checkedCategory = null;
            }
            if (String.IsNullOrWhiteSpace(category))
            {
                return;
            }
            for (int index = 1; index < _move.Items.Count; index++)
            {
                var item = _move.Items[index] as MenuItem;
                if (item != null && String.Equals(item.Tag as string, category, StringComparison.InvariantCultureIgnoreCase))
                {
                    _checkedCategory = item;
                    _checkedCategory.IsChecked = true;
                    break;
                }
            }
        }

        private void CopyBodyOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_copyBody);
        }

        private void CopyIdOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_copyId);
        }

        private void CopyJsonOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_copyJson);
        }

        private void ToggleFavoriteOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_toggleFavorite);
        }

        private void OpenStickyOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_openSticky);
        }

        private void SoftDeleteOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_softDelete);
        }

        private void RestoreOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_restore);
        }

        private void PermanentDeleteOnClick(object sender, RoutedEventArgs eventArgs)
        {
            InvokeForPreparedEntry(_permanentDelete);
        }

        private void MoveCategoryOnClick(object sender, RoutedEventArgs eventArgs)
        {
            var item = sender as MenuItem;
            if (item != null && _entry != null)
            {
                Entry entry = _entry;
                _entry = null;
                _moveEntry(entry, item.Tag as string);
            }
        }

        private void InvokeForPreparedEntry(Action<Entry> command)
        {
            Entry entry = _entry;
            if (entry == null)
            {
                return;
            }
            _entry = null;
            command(entry);
        }

        private void EntryContextMenuOnClosed(object sender, RoutedEventArgs eventArgs)
        {
            _entry = null;
        }
    }
}
