using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    internal sealed class CategoryListBox : ListBox
    {
        // Six rows fit without scrolling: 6 * 44 + 2 = 266 device-independent pixels.
        private const int DirectItemLimit = 6;
        private static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
            "Row",
            typeof(CategoryListRow),
            typeof(CategoryListBox));

        private ObservableCollection<CategoryListItem> _virtualItems;
        private HashSet<ListBoxItem> _realizedContainers;
        private bool _usesVirtualItems;
        private ListBoxItem _dropTarget;

        public ContextMenu RowContextMenu { get; set; }

        public string SelectedCategory
        {
            get { return CategoryFromItem(SelectedItem); }
        }

        public string CategoryFromContainer(ListBoxItem container)
        {
            if (container == null)
            {
                return null;
            }
            return _usesVirtualItems
                ? CategoryFromItem(ItemContainerGenerator.ItemFromContainer(container))
                : container.Tag as string;
        }

        public void RefreshItems(IList<string> categories, string selectedCategory, IDictionary<string, int> counts)
        {
            Height = categories.Count == 0 ? 0.0 : Math.Min(280.0, categories.Count * 44.0 + 2.0);
            bool shouldVirtualize = categories.Count > DirectItemLimit;
            if (shouldVirtualize != _usesVirtualItems)
            {
                SwitchMode(shouldVirtualize);
            }

            if (_usesVirtualItems)
            {
                RefreshVirtualItems(categories, selectedCategory, counts);
            }
            else
            {
                RefreshDirectItems(categories, selectedCategory, counts);
            }
        }

        public void SetDropTarget(ListBoxItem target)
        {
            if (ReferenceEquals(_dropTarget, target))
            {
                return;
            }
            ClearDropTarget();
            _dropTarget = target;
            if (_dropTarget != null)
            {
                _dropTarget.BorderBrush = Brush(ThemeResources.FocusBrushKey);
                _dropTarget.BorderThickness = new Thickness(1);
            }
        }

        public void ClearDropTarget()
        {
            if (_dropTarget == null)
            {
                return;
            }
            _dropTarget.ClearValue(Control.BorderBrushProperty);
            _dropTarget.ClearValue(Control.BorderThicknessProperty);
            _dropTarget = null;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (!_usesVirtualItems)
            {
                return;
            }
            var container = element as ListBoxItem;
            var category = item as CategoryListItem;
            if (container == null || category == null)
            {
                return;
            }

            _realizedContainers.Add(container);
            PrepareVirtualContainer(container, category);
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new CategoryListBoxItem(this);
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            if (!_usesVirtualItems)
            {
                base.ClearContainerForItemOverride(element, item);
                return;
            }
            var container = element as ListBoxItem;
            if (ReferenceEquals(_dropTarget, container))
            {
                ClearDropTarget();
            }

            var row = container == null ? null : container.GetValue(RowProperty) as CategoryListRow;
            if (row != null)
            {
                row.Update(null);
            }
            if (container != null)
            {
                if (_realizedContainers != null)
                {
                    _realizedContainers.Remove(container);
                }
                container.Tag = null;
                container.ContextMenu = null;
                container.ToolTip = null;
                AutomationProperties.SetName(container, String.Empty);
            }
            base.ClearContainerForItemOverride(element, item);
        }

        private void SwitchMode(bool useVirtualItems)
        {
            ClearDropTarget();
            SelectedItem = null;
            if (useVirtualItems)
            {
                Items.Clear();
                VirtualizingStackPanel.SetIsVirtualizing(this, true);
                VirtualizingStackPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
                ScrollViewer.SetCanContentScroll(this, true);
                if (_virtualItems == null)
                {
                    _virtualItems = new ObservableCollection<CategoryListItem>();
                    _realizedContainers = new HashSet<ListBoxItem>();
                }
                ItemsSource = _virtualItems;
                _usesVirtualItems = true;
                return;
            }

            ItemsSource = null;
            _virtualItems.Clear();
            _realizedContainers.Clear();
            ClearValue(VirtualizingStackPanel.IsVirtualizingProperty);
            ClearValue(VirtualizingStackPanel.VirtualizationModeProperty);
            ClearValue(ScrollViewer.CanContentScrollProperty);
            _usesVirtualItems = false;
        }

        private void RefreshVirtualItems(IList<string> categories, string selectedCategory, IDictionary<string, int> counts)
        {
            if (!MatchesVirtualItems(categories))
            {
                ReconcileVirtualItems(categories, counts);
            }

            CategoryListItem selected = null;
            for (int index = 0; index < _virtualItems.Count; index++)
            {
                CategoryListItem item = _virtualItems[index];
                item.Count = CountFor(item.Category, counts);
                if (String.Equals(item.Category, selectedCategory, StringComparison.InvariantCultureIgnoreCase))
                {
                    selected = item;
                }
            }
            RefreshRealizedContainers();
            SelectedItem = selected;
        }

        private bool MatchesVirtualItems(IList<string> categories)
        {
            if (_virtualItems.Count != categories.Count)
            {
                return false;
            }
            for (int index = 0; index < categories.Count; index++)
            {
                if (!String.Equals(_virtualItems[index].Category, categories[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private void ReconcileVirtualItems(IList<string> categories, IDictionary<string, int> counts)
        {
            var available = new Dictionary<string, CategoryListItem>(StringComparer.Ordinal);
            for (int index = 0; index < _virtualItems.Count; index++)
            {
                CategoryListItem item = _virtualItems[index];
                if (!available.ContainsKey(item.Category))
                {
                    available.Add(item.Category, item);
                }
            }

            var desired = new List<CategoryListItem>(categories.Count);
            var retained = new HashSet<CategoryListItem>();
            foreach (string category in categories)
            {
                CategoryListItem item;
                if (available.TryGetValue(category, out item))
                {
                    available.Remove(category);
                }
                else
                {
                    item = new CategoryListItem(category, CountFor(category, counts));
                }
                desired.Add(item);
                retained.Add(item);
            }

            for (int index = _virtualItems.Count - 1; index >= 0; index--)
            {
                if (!retained.Contains(_virtualItems[index]))
                {
                    _virtualItems.RemoveAt(index);
                }
            }
            foreach (CategoryListItem item in desired)
            {
                if (!_virtualItems.Contains(item))
                {
                    _virtualItems.Add(item);
                }
            }

            bool moveBackward = CountMoves(_virtualItems, desired, false) < CountMoves(_virtualItems, desired, true);
            ApplyVirtualOrder(desired, moveBackward);
        }

        private void ApplyVirtualOrder(IList<CategoryListItem> desired, bool backward)
        {
            int index = backward ? desired.Count - 1 : 0;
            int limit = backward ? -1 : desired.Count;
            int step = backward ? -1 : 1;
            for (; index != limit; index += step)
            {
                if (ReferenceEquals(_virtualItems[index], desired[index]))
                {
                    continue;
                }
                _virtualItems.Move(_virtualItems.IndexOf(desired[index]), index);
            }
        }

        private static int CountMoves(IList<CategoryListItem> current, IList<CategoryListItem> desired, bool forward)
        {
            var working = new List<CategoryListItem>(current);
            int moves = 0;
            int index = forward ? 0 : desired.Count - 1;
            int limit = forward ? desired.Count : -1;
            int step = forward ? 1 : -1;
            for (; index != limit; index += step)
            {
                if (ReferenceEquals(working[index], desired[index]))
                {
                    continue;
                }
                int sourceIndex = working.IndexOf(desired[index]);
                working.RemoveAt(sourceIndex);
                working.Insert(index, desired[index]);
                moves++;
            }
            return moves;
        }

        private void RefreshRealizedContainers()
        {
            foreach (ListBoxItem container in _realizedContainers)
            {
                var item = ItemContainerGenerator.ItemFromContainer(container) as CategoryListItem;
                if (item != null)
                {
                    PrepareVirtualContainer(container, item);
                }
            }
        }

        private void PrepareVirtualContainer(ListBoxItem container, CategoryListItem item)
        {
            var row = container.GetValue(RowProperty) as CategoryListRow;
            if (row == null)
            {
                row = new CategoryListRow(Brush(ThemeResources.MutedBrushKey));
                container.SetValue(RowProperty, row);
            }
            row.Update(item);
            container.Tag = item.Category;
            container.Content = row;
            container.ToolTip = TooltipFor(item.Category);
            container.ContextMenu = RowContextMenu;
            AutomationProperties.SetName(container, AccessibleNameFor(item.Category, item.Count));
        }

        private void RefreshDirectItems(IList<string> categories, string selectedCategory, IDictionary<string, int> counts)
        {
            if (!MatchesDirectItems(categories))
            {
                ReconcileDirectItems(categories, counts);
            }

            ListBoxItem selected = null;
            for (int index = 0; index < categories.Count; index++)
            {
                string category = categories[index];
                var item = (ListBoxItem)Items[index];
                UpdateDirectItem(item, category, CountFor(category, counts));
                if (String.Equals(category, selectedCategory, StringComparison.InvariantCultureIgnoreCase))
                {
                    selected = item;
                }
            }
            SelectedItem = selected;
        }

        private bool MatchesDirectItems(IList<string> categories)
        {
            if (Items.Count != categories.Count)
            {
                return false;
            }
            for (int index = 0; index < categories.Count; index++)
            {
                var item = Items[index] as ListBoxItem;
                if (item == null || !String.Equals(item.Tag as string, categories[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private void ReconcileDirectItems(IList<string> categories, IDictionary<string, int> counts)
        {
            var available = new Dictionary<string, ListBoxItem>(StringComparer.Ordinal);
            for (int index = 0; index < Items.Count; index++)
            {
                var item = Items[index] as ListBoxItem;
                string category = item == null ? null : item.Tag as string;
                if (category != null && !available.ContainsKey(category))
                {
                    available.Add(category, item);
                }
            }

            var desired = new List<ListBoxItem>(categories.Count);
            var retained = new HashSet<ListBoxItem>();
            var created = new List<ListBoxItem>();
            foreach (string category in categories)
            {
                ListBoxItem item;
                if (available.TryGetValue(category, out item))
                {
                    available.Remove(category);
                }
                else
                {
                    item = CreateDirectItem(category, CountFor(category, counts));
                    created.Add(item);
                }
                desired.Add(item);
                retained.Add(item);
            }

            for (int index = Items.Count - 1; index >= 0; index--)
            {
                var item = Items[index] as ListBoxItem;
                if (item == null || !retained.Contains(item))
                {
                    if (ReferenceEquals(_dropTarget, item))
                    {
                        ClearDropTarget();
                    }
                    Items.RemoveAt(index);
                }
            }
            foreach (ListBoxItem item in created)
            {
                Items.Add(item);
            }

            var current = new List<ListBoxItem>(Items.Count);
            for (int index = 0; index < Items.Count; index++)
            {
                current.Add((ListBoxItem)Items[index]);
            }
            bool moveBackward = CountDirectMoves(current, desired, false) < CountDirectMoves(current, desired, true);
            ApplyDirectOrder(desired, moveBackward);
        }

        private static int CountDirectMoves(IList<ListBoxItem> current, IList<ListBoxItem> desired, bool forward)
        {
            var working = new List<ListBoxItem>(current);
            int moves = 0;
            int index = forward ? 0 : desired.Count - 1;
            int limit = forward ? desired.Count : -1;
            int step = forward ? 1 : -1;
            for (; index != limit; index += step)
            {
                if (ReferenceEquals(working[index], desired[index]))
                {
                    continue;
                }
                int sourceIndex = IndexOfReference(working, desired[index]);
                working.RemoveAt(sourceIndex);
                working.Insert(index, desired[index]);
                moves++;
            }
            return moves;
        }

        private void ApplyDirectOrder(IList<ListBoxItem> desired, bool backward)
        {
            int index = backward ? desired.Count - 1 : 0;
            int limit = backward ? -1 : desired.Count;
            int step = backward ? -1 : 1;
            for (; index != limit; index += step)
            {
                if (ReferenceEquals(Items[index], desired[index]))
                {
                    continue;
                }
                int sourceIndex = Items.IndexOf(desired[index]);
                Items.RemoveAt(sourceIndex);
                Items.Insert(index, desired[index]);
            }
        }

        private static int IndexOfReference(IList<ListBoxItem> items, ListBoxItem target)
        {
            for (int index = 0; index < items.Count; index++)
            {
                if (ReferenceEquals(items[index], target))
                {
                    return index;
                }
            }
            throw new InvalidOperationException("Category row reconciliation lost an existing item.");
        }

        private ListBoxItem CreateDirectItem(string category, int count)
        {
            var item = new CategoryListBoxItem(this)
            {
                Tag = category,
                Content = CreateRow(category, count),
                ToolTip = TooltipFor(category),
                ContextMenu = RowContextMenu
            };
            AutomationProperties.SetName(item, AccessibleNameFor(category, count));
            return item;
        }

        internal void SelectContainer(ListBoxItem container)
        {
            if (container != null)
            {
                SelectedItem = _usesVirtualItems ? ItemContainerGenerator.ItemFromContainer(container) : container;
            }
        }

        private static Grid CreateRow(string category, int count)
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
                Tag = count,
                FontSize = 10,
                Foreground = Brush(ThemeResources.MutedBrushKey),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(countText, 1);
            row.Children.Add(countText);
            return row;
        }

        private static void UpdateDirectItem(ListBoxItem item, string category, int count)
        {
            var row = item.Content as Grid;
            var countText = row != null && row.Children.Count > 1 ? row.Children[1] as TextBlock : null;
            if (countText != null && (!(countText.Tag is int) || (int)countText.Tag != count))
            {
                countText.Text = count.ToString();
                countText.Tag = count;
                AutomationProperties.SetName(item, AccessibleNameFor(category, count));
            }
        }

        private static string CategoryFromItem(object item)
        {
            var virtualItem = item as CategoryListItem;
            if (virtualItem != null)
            {
                return virtualItem.Category;
            }
            var directItem = item as ListBoxItem;
            return directItem == null ? null : directItem.Tag as string;
        }

        private static int CountFor(string category, IDictionary<string, int> counts)
        {
            int count;
            return counts != null && counts.TryGetValue(category, out count) ? count : 0;
        }

        private static string TooltipFor(string category)
        {
            return "拖动排序；将 Note 拖到这里可移动到“" + category + "”";
        }

        private static string AccessibleNameFor(string category, int count)
        {
            return category + "，" + count + " 条 Note";
        }

        private static Brush Brush(string key)
        {
            return (Brush)Application.Current.FindResource(key);
        }
    }

    internal sealed class CategoryListBoxItem : ListBoxItem
    {
        private readonly CategoryListBox _owner;

        public CategoryListBoxItem(CategoryListBox owner)
        {
            _owner = owner;
            Style = (Style)Application.Current.FindResource(typeof(ListBoxItem));
        }

        protected override void OnPreviewMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs eventArgs)
        {
            _owner.SelectContainer(this);
            base.OnPreviewMouseRightButtonDown(eventArgs);
        }
    }

    internal sealed class CategoryListItem
    {
        public CategoryListItem(string category, int count)
        {
            Category = category;
            Count = count;
        }

        public string Category { get; private set; }
        public int Count { get; set; }

        public override string ToString()
        {
            return Category;
        }
    }

    internal sealed class CategoryListRow : Grid
    {
        private readonly TextBlock _category;
        private readonly TextBlock _count;

        public CategoryListRow(Brush mutedBrush)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _category = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            _count = new TextBlock
            {
                FontSize = 10,
                Foreground = mutedBrush,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Children.Add(_category);
            SetColumn(_count, 1);
            Children.Add(_count);
        }

        public void Update(CategoryListItem item)
        {
            if (item == null)
            {
                _category.Text = String.Empty;
                _count.Text = String.Empty;
                _count.Tag = null;
                return;
            }

            if (!String.Equals(_category.Text, item.Category, StringComparison.Ordinal))
            {
                _category.Text = item.Category;
            }
            if (!(_count.Tag is int) || (int)_count.Tag != item.Count)
            {
                _count.Text = item.Count.ToString();
                _count.Tag = item.Count;
            }
        }
    }
}
