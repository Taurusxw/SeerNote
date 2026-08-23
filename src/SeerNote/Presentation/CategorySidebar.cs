using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    public sealed class CategoryNameEventArgs : EventArgs
    {
        public CategoryNameEventArgs(string category)
        {
            Category = category;
        }

        public string Category { get; private set; }
    }

    public sealed class CategoryReorderEventArgs : EventArgs
    {
        public CategoryReorderEventArgs(string category, string targetCategory, bool insertAfter)
        {
            Category = category;
            TargetCategory = targetCategory;
            InsertAfter = insertAfter;
        }

        public string Category { get; private set; }
        public string TargetCategory { get; private set; }
        public bool InsertAfter { get; private set; }
    }

    public sealed class EntryCategoryMoveEventArgs : EventArgs
    {
        public EntryCategoryMoveEventArgs(Guid entryId, string category)
        {
            EntryId = entryId;
            Category = category;
        }

        public Guid EntryId { get; private set; }
        public string Category { get; private set; }
    }

    public sealed class CategorySidebar : StackPanel
    {
        public const string CategoryDragFormat = "SeerNote.Category";
        public const string EntryDragFormat = "SeerNote.EntryId";

        private CategoryListBox _list;
        private bool _refreshing;
        private Point _dragStart;
        private string _dragCategory;
        private ContextMenu _contextMenu;

        public CategorySidebar()
        {
            var header = new Grid { Margin = new Thickness(1, 14, 0, 6) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock
            {
                Text = "自定义分类",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                VerticalAlignment = VerticalAlignment.Center
            });
            var add = new Button
            {
                Content = "＋ 新建",
                MinHeight = 24,
                Padding = new Thickness(6, 2, 6, 2),
                Style = (Style)Application.Current.FindResource("Seer.QuietButton")
            };
            AutomationProperties.SetName(add, "新建分类");
            add.Click += delegate { Raise(CreateRequested); };
            Grid.SetColumn(add, 1);
            header.Children.Add(add);
            Children.Add(header);

        }

        private CategoryListBox EnsureList()
        {
            if (_list != null)
            {
                return _list;
            }

            _list = new CategoryListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxHeight = 280,
                Margin = new Thickness(0),
                AllowDrop = true
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
            AutomationProperties.SetName(_list, "自定义分类列表");
            AutomationProperties.SetHelpText(_list, "选择分类筛选 Note；拖动分类可排序，也可把 Note 拖到分类中移动。");
            _list.SelectionChanged += ListOnSelectionChanged;
            _list.PreviewMouseLeftButtonDown += ListOnPreviewMouseLeftButtonDown;
            _list.PreviewMouseMove += ListOnPreviewMouseMove;
            _list.PreviewDragOver += ListOnPreviewDragOver;
            _list.Drop += ListOnDrop;
            _list.DragLeave += delegate { _list.ClearDropTarget(); };
            Children.Add(_list);
            return _list;
        }

        public event EventHandler CreateRequested;
        public event EventHandler<CategoryNameEventArgs> CategorySelected;
        public event EventHandler<CategoryNameEventArgs> RenameRequested;
        public event EventHandler<CategoryNameEventArgs> DeleteRequested;
        public event EventHandler<CategoryReorderEventArgs> ReorderRequested;
        public event EventHandler<EntryCategoryMoveEventArgs> EntryMoveRequested;

        public void Refresh(IList<string> categories, string selectedCategory, IDictionary<string, int> counts)
        {
            if (categories == null)
            {
                throw new ArgumentNullException(nameof(categories));
            }

            _refreshing = true;
            try
            {
                if (categories.Count == 0 && _list == null)
                {
                    return;
                }
                CategoryListBox list = EnsureList();
                if (categories.Count == 0 && list.ItemsSource == null && list.Items.Count == 0)
                {
                    list.Height = 0.0;
                    list.SelectedItem = null;
                    return;
                }
                if (categories.Count > 0 && list.RowContextMenu == null)
                {
                    list.RowContextMenu = GetContextMenu();
                }
                list.RefreshItems(categories, selectedCategory, counts);
            }
            finally
            {
                _refreshing = false;
            }
        }

        private ContextMenu GetContextMenu()
        {
            if (_contextMenu != null)
            {
                return _contextMenu;
            }

            _contextMenu = new ContextMenu();
            _contextMenu.Opened += ContextMenuOnOpened;
            return _contextMenu;
        }

        private void ContextMenuOnOpened(object sender, RoutedEventArgs eventArgs)
        {
            if (_contextMenu.Items.Count > 0)
            {
                return;
            }
            var rename = new MenuItem { Header = "重命名分类" };
            rename.Click += delegate { RaiseContextMenuAction(RenameRequested); };
            var delete = new MenuItem { Header = "删除分类" };
            delete.Click += delegate { RaiseContextMenuAction(DeleteRequested); };
            _contextMenu.Items.Add(rename);
            _contextMenu.Items.Add(delete);
        }

        private void RaiseContextMenuAction(EventHandler<CategoryNameEventArgs> handler)
        {
            var item = _contextMenu == null ? null : _contextMenu.PlacementTarget as ListBoxItem;
            string category = _list.CategoryFromContainer(item);
            if (category != null)
            {
                Raise(handler, new CategoryNameEventArgs(category));
            }
        }

        private void ListOnSelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            if (_refreshing)
            {
                return;
            }
            string category = _list.SelectedCategory;
            if (category != null)
            {
                Raise(CategorySelected, new CategoryNameEventArgs(category));
            }
        }

        private void ListOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
        {
            _dragStart = eventArgs.GetPosition(_list);
            var item = ItemsControl.ContainerFromElement(_list, eventArgs.OriginalSource as DependencyObject) as ListBoxItem;
            _dragCategory = _list.CategoryFromContainer(item);
        }

        private void ListOnPreviewMouseMove(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.LeftButton != MouseButtonState.Pressed || _dragCategory == null)
            {
                return;
            }
            Point current = eventArgs.GetPosition(_list);
            if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            string category = _dragCategory;
            _dragCategory = null;
            DragDrop.DoDragDrop(_list, new DataObject(CategoryDragFormat, category), DragDropEffects.Move);
        }

        private void ListOnPreviewDragOver(object sender, DragEventArgs eventArgs)
        {
            ListBoxItem target = ItemsControl.ContainerFromElement(_list, eventArgs.OriginalSource as DependencyObject) as ListBoxItem;
            bool supported = target != null
                && (eventArgs.Data.GetDataPresent(CategoryDragFormat) || eventArgs.Data.GetDataPresent(EntryDragFormat));
            eventArgs.Effects = supported ? DragDropEffects.Move : DragDropEffects.None;
            if (supported)
            {
                _list.SetDropTarget(target);
            }
            else
            {
                _list.ClearDropTarget();
            }
            eventArgs.Handled = true;
        }

        private void ListOnDrop(object sender, DragEventArgs eventArgs)
        {
            ListBoxItem target = ItemsControl.ContainerFromElement(_list, eventArgs.OriginalSource as DependencyObject) as ListBoxItem;
            string targetCategory = _list.CategoryFromContainer(target);
            if (targetCategory != null && eventArgs.Data.GetDataPresent(EntryDragFormat))
            {
                string rawId = eventArgs.Data.GetData(EntryDragFormat) as string;
                Guid entryId;
                if (Guid.TryParse(rawId, out entryId))
                {
                    Raise(EntryMoveRequested, new EntryCategoryMoveEventArgs(entryId, targetCategory));
                }
            }
            else if (targetCategory != null && eventArgs.Data.GetDataPresent(CategoryDragFormat))
            {
                string category = eventArgs.Data.GetData(CategoryDragFormat) as string;
                bool insertAfter = eventArgs.GetPosition(target).Y > target.ActualHeight / 2;
                if (!String.IsNullOrWhiteSpace(category) && !String.Equals(category, targetCategory, StringComparison.InvariantCultureIgnoreCase))
                {
                    Raise(ReorderRequested, new CategoryReorderEventArgs(category, targetCategory, insertAfter));
                }
            }
            _list.ClearDropTarget();
            eventArgs.Handled = true;
        }

        private static Brush Brush(string key)
        {
            return (Brush)Application.Current.FindResource(key);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void Raise<TEventArgs>(EventHandler<TEventArgs> handler, TEventArgs eventArgs) where TEventArgs : EventArgs
        {
            if (handler != null)
            {
                handler(this, eventArgs);
            }
        }
    }
}
