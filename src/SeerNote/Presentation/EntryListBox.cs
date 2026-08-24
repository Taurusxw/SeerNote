using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SeerNote.Domain;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    internal sealed class EntryReorderEventArgs : EventArgs
    {
        public EntryReorderEventArgs(Guid entryId, Guid targetEntryId, bool insertAfter)
        {
            EntryId = entryId;
            TargetEntryId = targetEntryId;
            InsertAfter = insertAfter;
        }

        public Guid EntryId { get; private set; }
        public Guid TargetEntryId { get; private set; }
        public bool InsertAfter { get; private set; }
    }

    internal sealed class EntryListBox : ListBox
    {
        public const string ReorderDragFormat = "SeerNote.EntryOrder";

        private static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
            "Row",
            typeof(EntryListRow),
            typeof(EntryListBox));
        private static readonly ConditionalWeakTable<EntryListBox, ContextMenuCache> ContextMenus = new ConditionalWeakTable<EntryListBox, ContextMenuCache>();
        private ListBoxItem _dropTarget;
        private EntryInsertionAdorner _dropAdorner;
        private bool _dropInsertAfter;
        private ScrollViewer _scrollViewer;

        public EntryListBox()
        {
            VirtualizingStackPanel.SetIsVirtualizing(this, true);
            VirtualizingStackPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            ScrollViewer.SetCanContentScroll(this, true);
            AllowDrop = true;
            PreviewDragOver += EntryListBoxOnPreviewDragOver;
            Drop += EntryListBoxOnDrop;
            DragLeave += EntryListBoxOnDragLeave;
        }

        public Func<Entry, ContextMenu> ContextMenuFactory { get; set; }

        public event EventHandler<EntryReorderEventArgs> EntryReorderRequested;

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            var container = element as ListBoxItem;
            var entry = item as Entry;
            if (container == null || entry == null)
            {
                return;
            }

            var row = container.GetValue(RowProperty) as EntryListRow;
            if (row == null)
            {
                row = new EntryListRow();
                container.SetValue(RowProperty, row);
            }
            row.Update(entry);
            container.Content = row;
            container.Margin = new Thickness(0, 0, 0, 3);
            container.ToolTip = EntryListRow.TitleFor(entry);
            container.ContextMenu = GetContextMenu(entry);
            AutomationProperties.SetName(container, "Note " + EntryListRow.TitleFor(entry));
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            var container = element as ListBoxItem;
            if (ReferenceEquals(_dropTarget, container))
            {
                ClearDropTarget();
            }
            var row = container == null ? null : container.GetValue(RowProperty) as EntryListRow;
            if (row != null)
            {
                row.Update(null);
            }
            if (container != null)
            {
                container.ContextMenu = null;
                container.ToolTip = null;
                AutomationProperties.SetName(container, String.Empty);
            }
            base.ClearContainerForItemOverride(element, item);
        }

        private ContextMenu GetContextMenu(Entry entry)
        {
            if (ContextMenuFactory == null)
            {
                return null;
            }
            if (Items.Count <= 6)
            {
                return ContextMenuFactory(entry);
            }
            ContextMenuCache cache = ContextMenus.GetOrCreateValue(this);
            if (entry.IsDeleted)
            {
                if (cache.Deleted == null)
                {
                    cache.Deleted = ContextMenuFactory(entry);
                }
                return cache.Deleted;
            }
            if (cache.Active == null)
            {
                cache.Active = ContextMenuFactory(entry);
            }
            return cache.Active;
        }

        protected override void OnContextMenuOpening(ContextMenuEventArgs eventArgs)
        {
            base.OnContextMenuOpening(eventArgs);
            var source = eventArgs.OriginalSource as DependencyObject;
            var container = ItemsControl.ContainerFromElement(this, source) as ListBoxItem;
            var entry = container == null ? null : ItemContainerGenerator.ItemFromContainer(container) as Entry;
            ContextMenu menu = container == null ? null : container.ContextMenu;
            if (entry == null || menu == null)
            {
                return;
            }
            if (!ReferenceEquals(SelectedItem, entry))
            {
                SelectedItem = entry;
            }
            var sharedMenu = menu as EntryContextMenu;
            if (sharedMenu != null)
            {
                sharedMenu.Prepare(entry);
                return;
            }
            menu.Tag = entry;
        }

        protected override void OnContextMenuClosing(ContextMenuEventArgs eventArgs)
        {
            var source = eventArgs.OriginalSource as DependencyObject;
            var container = ItemsControl.ContainerFromElement(this, source) as ListBoxItem;
            ContextMenu menu = container == null ? null : container.ContextMenu;
            if (menu != null && !(menu is EntryContextMenu))
            {
                menu.Tag = null;
            }
            base.OnContextMenuClosing(eventArgs);
        }

        private void EntryListBoxOnPreviewDragOver(object sender, DragEventArgs eventArgs)
        {
            ListBoxItem target = ContainerAt(eventArgs);
            Guid entryId;
            Entry source;
            Entry targetEntry;
            bool supported = TryGetReorder(eventArgs.Data, target, out entryId, out source, out targetEntry);
            eventArgs.Effects = supported ? DragDropEffects.Move : DragDropEffects.None;
            if (supported)
            {
                bool insertAfter = eventArgs.GetPosition(target).Y > target.ActualHeight / 2.0;
                SetDropTarget(target, insertAfter);
                AutoScroll(eventArgs.GetPosition(this));
            }
            else
            {
                ClearDropTarget();
            }
            eventArgs.Handled = true;
        }

        private void EntryListBoxOnDrop(object sender, DragEventArgs eventArgs)
        {
            ListBoxItem target = ContainerAt(eventArgs);
            Guid entryId;
            Entry source;
            Entry targetEntry;
            if (TryGetReorder(eventArgs.Data, target, out entryId, out source, out targetEntry))
            {
                bool insertAfter = eventArgs.GetPosition(target).Y > target.ActualHeight / 2.0;
                EventHandler<EntryReorderEventArgs> handler = EntryReorderRequested;
                if (handler != null)
                {
                    handler(this, new EntryReorderEventArgs(entryId, targetEntry.Id, insertAfter));
                }
                eventArgs.Effects = DragDropEffects.Move;
            }
            else
            {
                eventArgs.Effects = DragDropEffects.None;
            }
            ClearDropTarget();
            eventArgs.Handled = true;
        }

        private void EntryListBoxOnDragLeave(object sender, DragEventArgs eventArgs)
        {
            ClearDropTarget();
        }

        private ListBoxItem ContainerAt(DragEventArgs eventArgs)
        {
            DependencyObject source = eventArgs.OriginalSource as DependencyObject;
            ListBoxItem container = ItemsControl.ContainerFromElement(this, source) as ListBoxItem;
            if (container != null)
            {
                return container;
            }
            DependencyObject hit = InputHitTest(eventArgs.GetPosition(this)) as DependencyObject;
            return ItemsControl.ContainerFromElement(this, hit) as ListBoxItem;
        }

        private bool TryGetReorder(IDataObject data, ListBoxItem target, out Guid entryId, out Entry source, out Entry targetEntry)
        {
            entryId = Guid.Empty;
            source = null;
            targetEntry = target == null ? null : ItemContainerGenerator.ItemFromContainer(target) as Entry;
            if (data == null || targetEntry == null || !data.GetDataPresent(ReorderDragFormat))
            {
                return false;
            }
            string rawId = data.GetData(ReorderDragFormat) as string;
            Guid parsedId;
            if (!Guid.TryParse(rawId, out parsedId) || parsedId == targetEntry.Id)
            {
                return false;
            }
            entryId = parsedId;
            source = Items.OfType<Entry>().FirstOrDefault(entry => entry != null && entry.Id == parsedId);
            return source != null && EntryOrder.IsSameGroup(source, targetEntry);
        }

        private void SetDropTarget(ListBoxItem target, bool insertAfter)
        {
            if (ReferenceEquals(_dropTarget, target) && _dropInsertAfter == insertAfter)
            {
                return;
            }
            ClearDropTarget();
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null)
            {
                return;
            }
            _dropTarget = target;
            _dropInsertAfter = insertAfter;
            _dropAdorner = new EntryInsertionAdorner(target, insertAfter);
            layer.Add(_dropAdorner);
        }

        private void ClearDropTarget()
        {
            if (_dropTarget != null && _dropAdorner != null)
            {
                AdornerLayer layer = AdornerLayer.GetAdornerLayer(_dropTarget);
                if (layer != null)
                {
                    layer.Remove(_dropAdorner);
                }
            }
            _dropTarget = null;
            _dropAdorner = null;
        }

        private void AutoScroll(Point position)
        {
            if (_scrollViewer == null)
            {
                _scrollViewer = FindVisualChild<ScrollViewer>(this);
            }
            if (_scrollViewer == null)
            {
                return;
            }
            const double edge = 28.0;
            if (position.Y < edge)
            {
                _scrollViewer.LineUp();
            }
            else if (position.Y > ActualHeight - edge)
            {
                _scrollViewer.LineDown();
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < count; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                T match = child as T;
                if (match != null)
                {
                    return match;
                }
                match = FindVisualChild<T>(child);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private sealed class ContextMenuCache
        {
            public ContextMenu Active { get; set; }

            public ContextMenu Deleted { get; set; }
        }
    }

    internal sealed class EntryInsertionAdorner : Adorner
    {
        private readonly bool _insertAfter;

        public EntryInsertionAdorner(UIElement adornedElement, bool insertAfter) : base(adornedElement)
        {
            _insertAfter = insertAfter;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            Brush brush = Application.Current == null
                ? Brushes.DeepSkyBlue
                : Application.Current.TryFindResource(ThemeResources.FocusBrushKey) as Brush ?? Brushes.DeepSkyBlue;
            var pen = new Pen(brush, 2.0);
            double y = _insertAfter ? Math.Max(0.0, AdornedElement.RenderSize.Height - 1.0) : 1.0;
            double right = Math.Max(4.0, AdornedElement.RenderSize.Width - 4.0);
            drawingContext.DrawLine(pen, new Point(4.0, y), new Point(right, y));
        }
    }

    internal sealed class EntryListRow : Grid
    {
        private readonly TextBlock _title;
        private readonly TextBlock _marker;
        private readonly TextBlock _preview;
        private readonly TextBlock _meta;

        public EntryListRow()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _title = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Children.Add(_title);

            _marker = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(8, 1, 0, 0)
            };
            _marker.SetResourceReference(TextBlock.ForegroundProperty, ThemeResources.GoldBrushKey);
            SetColumn(_marker, 1);
            Children.Add(_marker);

            _preview = new TextBlock
            {
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 4, 4, 0)
            };
            _preview.SetResourceReference(TextBlock.ForegroundProperty, ThemeResources.MutedBrushKey);
            SetRow(_preview, 1);
            SetColumnSpan(_preview, 2);
            Children.Add(_preview);

            _meta = new TextBlock
            {
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _meta.SetResourceReference(TextBlock.ForegroundProperty, ThemeResources.MutedBrushKey);
            SetRow(_meta, 2);
            SetColumnSpan(_meta, 2);
            Children.Add(_meta);
        }

        public void Update(Entry entry)
        {
            if (entry == null)
            {
                _title.Text = String.Empty;
                _marker.Text = String.Empty;
                _preview.Text = String.Empty;
                _meta.Text = String.Empty;
                AutomationProperties.SetName(this, String.Empty);
                return;
            }

            string title = TitleFor(entry);
            string category = String.IsNullOrWhiteSpace(entry.Category) ? "未分类" : entry.Category.Trim();
            _title.Text = title;
            _marker.Text = entry.IsFavorite ? "★" : String.Empty;
            _preview.Text = PreviewFor(entry.Body);
            _meta.Text = category + "  ·  " + entry.UpdatedUtc.ToLocalTime().ToString("MM-dd HH:mm");
            AutomationProperties.SetName(this, "Note " + title);
        }

        public static string TitleFor(Entry entry)
        {
            string value = entry == null ? String.Empty : entry.DisplayTitle;
            return String.IsNullOrWhiteSpace(value) ? "未命名" : Truncate(value, 80);
        }

        private static string PreviewFor(string body)
        {
            string value = String.Join(" ", (body ?? String.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(part => part.Trim()).Where(part => part.Length > 0));
            return value.Length == 0 ? "（正文为空）" : Truncate(value, 90);
        }

        private static string Truncate(string value, int maximum)
        {
            value = value ?? String.Empty;
            return value.Length <= maximum ? value : value.Substring(0, Math.Max(0, maximum - 1)) + "…";
        }
    }
}
