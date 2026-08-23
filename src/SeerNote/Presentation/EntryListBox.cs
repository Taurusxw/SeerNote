using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SeerNote.Domain;
using SeerNote.Theme;

namespace SeerNote.Presentation
{
    internal sealed class EntryListBox : ListBox
    {
        private static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
            "Row",
            typeof(EntryListRow),
            typeof(EntryListBox));
        private static readonly ConditionalWeakTable<EntryListBox, ContextMenuCache> ContextMenus = new ConditionalWeakTable<EntryListBox, ContextMenuCache>();

        public EntryListBox()
        {
            VirtualizingStackPanel.SetIsVirtualizing(this, true);
            VirtualizingStackPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            ScrollViewer.SetCanContentScroll(this, true);
        }

        public Func<Entry, ContextMenu> ContextMenuFactory { get; set; }

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

        private sealed class ContextMenuCache
        {
            public ContextMenu Active { get; set; }

            public ContextMenu Deleted { get; set; }
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
