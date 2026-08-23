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

        private readonly ListBox _list;
        private bool _refreshing;
        private Point _dragStart;
        private string _dragCategory;
        private ListBoxItem _dropTarget;

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

            _list = new ListBox
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
            _list.DragLeave += delegate { ClearDropTarget(); };
            Children.Add(_list);
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
                _list.Height = categories.Count == 0 ? 0.0 : Math.Min(280.0, categories.Count * 44.0 + 2.0);
                bool rebuild = !MatchesCategories(categories);
                if (rebuild)
                {
                    _list.Items.Clear();
                    foreach (string category in categories)
                    {
                        _list.Items.Add(CreateItem(category, CountFor(category, counts)));
                    }
                }

                ListBoxItem selected = null;
                for (int index = 0; index < categories.Count; index++)
                {
                    string category = categories[index];
                    var item = (ListBoxItem)_list.Items[index];
                    UpdateItem(item, category, CountFor(category, counts));
                    if (String.Equals(category, selectedCategory, StringComparison.InvariantCultureIgnoreCase))
                    {
                        selected = item;
                    }
                }
                _list.SelectedItem = selected;
            }
            finally
            {
                _refreshing = false;
            }
        }

        private bool MatchesCategories(IList<string> categories)
        {
            if (_list.Items.Count != categories.Count)
            {
                return false;
            }
            for (int index = 0; index < categories.Count; index++)
            {
                var item = _list.Items[index] as ListBoxItem;
                if (item == null || !String.Equals(item.Tag as string, categories[index], StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        private static int CountFor(string category, IDictionary<string, int> counts)
        {
            int count;
            return counts != null && counts.TryGetValue(category, out count) ? count : 0;
        }

        private ListBoxItem CreateItem(string category, int count)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = category,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            var countText = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 10,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(countText, 1);
            row.Children.Add(countText);

            var item = new ListBoxItem
            {
                Tag = category,
                Content = row,
                ToolTip = "拖动排序；将 Note 拖到这里可移动到“" + category + "”"
            };
            AutomationProperties.SetName(item, category + "，" + count + " 条 Note");
            item.PreviewMouseRightButtonDown += delegate { _list.SelectedItem = item; };
            item.ContextMenu = CreateContextMenu(category);
            return item;
        }

        private static void UpdateItem(ListBoxItem item, string category, int count)
        {
            var row = item.Content as Grid;
            var countText = row != null && row.Children.Count > 1 ? row.Children[1] as TextBlock : null;
            if (countText != null)
            {
                countText.Text = count.ToString();
            }
            AutomationProperties.SetName(item, category + "，" + count + " 条 Note");
        }

        private ContextMenu CreateContextMenu(string category)
        {
            var menu = new ContextMenu();
            var rename = new MenuItem { Header = "重命名分类" };
            rename.Click += delegate { Raise(RenameRequested, new CategoryNameEventArgs(category)); };
            var delete = new MenuItem { Header = "删除分类" };
            delete.Click += delegate { Raise(DeleteRequested, new CategoryNameEventArgs(category)); };
            menu.Items.Add(rename);
            menu.Items.Add(delete);
            return menu;
        }

        private void ListOnSelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            if (_refreshing)
            {
                return;
            }
            var item = _list.SelectedItem as ListBoxItem;
            string category = item == null ? null : item.Tag as string;
            if (category != null)
            {
                Raise(CategorySelected, new CategoryNameEventArgs(category));
            }
        }

        private void ListOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
        {
            _dragStart = eventArgs.GetPosition(_list);
            var item = ItemsControl.ContainerFromElement(_list, eventArgs.OriginalSource as DependencyObject) as ListBoxItem;
            _dragCategory = item == null ? null : item.Tag as string;
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
                SetDropTarget(target);
            }
            else
            {
                ClearDropTarget();
            }
            eventArgs.Handled = true;
        }

        private void ListOnDrop(object sender, DragEventArgs eventArgs)
        {
            ListBoxItem target = ItemsControl.ContainerFromElement(_list, eventArgs.OriginalSource as DependencyObject) as ListBoxItem;
            string targetCategory = target == null ? null : target.Tag as string;
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
            ClearDropTarget();
            eventArgs.Handled = true;
        }

        private void SetDropTarget(ListBoxItem target)
        {
            if (ReferenceEquals(_dropTarget, target))
            {
                return;
            }
            ClearDropTarget();
            _dropTarget = target;
            _dropTarget.BorderBrush = Brush(ThemeResources.FocusBrushKey);
            _dropTarget.BorderThickness = new Thickness(1);
        }

        private void ClearDropTarget()
        {
            if (_dropTarget == null)
            {
                return;
            }
            _dropTarget.ClearValue(Control.BorderBrushProperty);
            _dropTarget.ClearValue(Control.BorderThicknessProperty);
            _dropTarget = null;
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
