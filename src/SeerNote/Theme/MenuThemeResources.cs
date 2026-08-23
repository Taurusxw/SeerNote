using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SeerNote.Theme
{
    /// <summary>
    /// Owns the complete WPF menu surface so every current and future menu uses
    /// the same chrome, nested-popup palette, check column and interaction states.
    /// </summary>
    internal static class MenuThemeResources
    {
        public static void Add(
            ResourceDictionary resources,
            Brush ink,
            Brush muted,
            Brush surface,
            Brush raisedSurface,
            Brush border,
            Brush accent,
            Brush accentHover,
            Brush accentInk,
            FontFamily uiFont)
        {
            resources[SystemColors.MenuBrushKey] = raisedSurface;
            resources[SystemColors.MenuBarBrushKey] = raisedSurface;
            resources[SystemColors.MenuTextBrushKey] = ink;
            resources[SystemColors.HighlightBrushKey] = accent;
            resources[SystemColors.HighlightTextBrushKey] = accentInk;
            resources[SystemColors.GrayTextBrushKey] = muted;

            var menuItemStyle = new Style(typeof(MenuItem));
            menuItemStyle.Setters.Add(new Setter(FrameworkElement.OverridesDefaultStyleProperty, true));
            menuItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            menuItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            menuItemStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            menuItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            menuItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 5, 8, 5)));
            menuItemStyle.Setters.Add(new Setter(Control.MinHeightProperty, 30.0));
            menuItemStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            menuItemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            menuItemStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateMenuItemTemplate(raisedSurface, surface, border, accentHover, ink)));

            var separatorStyle = new Style(typeof(Separator));
            separatorStyle.Setters.Add(new Setter(FrameworkElement.OverridesDefaultStyleProperty, true));
            separatorStyle.Setters.Add(new Setter(Control.BackgroundProperty, border));
            separatorStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 1.0));
            separatorStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(7, 4, 7, 4)));
            separatorStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateSeparatorTemplate()));

            var containerStyleSelector = new MenuContainerStyleSelector(menuItemStyle, separatorStyle);
            menuItemStyle.Setters.Add(new Setter(ItemsControl.ItemContainerStyleSelectorProperty, containerStyleSelector));
            resources[typeof(MenuItem)] = menuItemStyle;
            resources[typeof(Separator)] = separatorStyle;

            var contextMenuStyle = new Style(typeof(ContextMenu));
            contextMenuStyle.Setters.Add(new Setter(FrameworkElement.OverridesDefaultStyleProperty, true));
            contextMenuStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            contextMenuStyle.Setters.Add(new Setter(Control.BackgroundProperty, raisedSurface));
            contextMenuStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            contextMenuStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            contextMenuStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            contextMenuStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            contextMenuStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3)));
            contextMenuStyle.Setters.Add(new Setter(ContextMenu.HasDropShadowProperty, true));
            contextMenuStyle.Setters.Add(new Setter(ItemsControl.ItemContainerStyleSelectorProperty, containerStyleSelector));
            contextMenuStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateContextMenuTemplate()));
            resources[typeof(ContextMenu)] = contextMenuStyle;

            var menuStyle = new Style(typeof(Menu));
            menuStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            menuStyle.Setters.Add(new Setter(Control.BackgroundProperty, raisedSurface));
            menuStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            menuStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            menuStyle.Setters.Add(new Setter(ItemsControl.ItemContainerStyleSelectorProperty, containerStyleSelector));
            resources[typeof(Menu)] = menuStyle;
        }

        private sealed class MenuContainerStyleSelector : StyleSelector
        {
            private readonly Style _menuItemStyle;
            private readonly Style _separatorStyle;

            public MenuContainerStyleSelector(Style menuItemStyle, Style separatorStyle)
            {
                _menuItemStyle = menuItemStyle;
                _separatorStyle = separatorStyle;
            }

            public override Style SelectStyle(object item, DependencyObject container)
            {
                if (container is Separator || item is Separator)
                {
                    return _separatorStyle;
                }
                if (container is MenuItem || item is MenuItem)
                {
                    return _menuItemStyle;
                }
                return null;
            }
        }

        private static ControlTemplate CreateContextMenuTemplate()
        {
            var template = new ControlTemplate(typeof(ContextMenu));
            var border = new FrameworkElementFactory(typeof(Border), "MenuBorder");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroll.AppendChild(items);
            border.AppendChild(scroll);
            template.VisualTree = border;
            return template;
        }

        private static ControlTemplate CreateMenuItemTemplate(
            Brush raisedSurface,
            Brush surface,
            Brush border,
            Brush accentHover,
            Brush ink)
        {
            var template = new ControlTemplate(typeof(MenuItem));
            var root = new FrameworkElementFactory(typeof(Grid), "MenuItemRoot");
            root.SetValue(FrameworkElement.SnapsToDevicePixelsProperty, true);

            var itemBorder = new FrameworkElementFactory(typeof(Border), "ItemBorder");
            itemBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            itemBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            itemBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            var row = new FrameworkElementFactory(typeof(DockPanel));
            row.SetValue(DockPanel.LastChildFillProperty, true);

            var arrow = new FrameworkElementFactory(typeof(TextBlock), "SubmenuArrow");
            arrow.SetValue(TextBlock.TextProperty, "›");
            arrow.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
            arrow.SetValue(TextBlock.FontSizeProperty, 16.0);
            arrow.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            arrow.SetValue(FrameworkElement.WidthProperty, 16.0);
            arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(8, -1, 0, 0));
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(UIElement.VisibilityProperty, Visibility.Hidden);
            arrow.SetValue(DockPanel.DockProperty, Dock.Right);
            row.AppendChild(arrow);

            var gesture = new FrameworkElementFactory(typeof(TextBlock));
            gesture.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(MenuItem.InputGestureTextProperty));
            gesture.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
            gesture.SetValue(FrameworkElement.MarginProperty, new Thickness(18, 0, 2, 0));
            gesture.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            gesture.SetValue(DockPanel.DockProperty, Dock.Right);
            row.AppendChild(gesture);

            var iconHost = new FrameworkElementFactory(typeof(Grid));
            iconHost.SetValue(FrameworkElement.WidthProperty, 20.0);
            iconHost.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
            iconHost.SetValue(DockPanel.DockProperty, Dock.Left);
            var icon = new FrameworkElementFactory(typeof(ContentPresenter));
            icon.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(MenuItem.IconProperty));
            icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            iconHost.AppendChild(icon);
            var check = new FrameworkElementFactory(typeof(TextBlock), "CheckGlyph");
            check.SetValue(TextBlock.TextProperty, "✓");
            check.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
            check.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Symbol"));
            check.SetValue(TextBlock.FontSizeProperty, 13.0);
            check.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetValue(UIElement.VisibilityProperty, Visibility.Hidden);
            iconHost.AppendChild(check);
            row.AppendChild(iconHost);

            var header = new FrameworkElementFactory(typeof(ContentPresenter));
            header.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(HeaderedItemsControl.HeaderProperty));
            header.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(HeaderedItemsControl.HeaderTemplateProperty));
            header.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(HeaderedItemsControl.HeaderStringFormatProperty));
            header.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            header.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            row.AppendChild(header);
            itemBorder.AppendChild(row);
            root.AppendChild(itemBorder);

            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.FocusableProperty, false);
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Right);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.None);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsSubmenuOpen")
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.OneWay
            });
            popup.SetBinding(Popup.PlacementTargetProperty, new Binding
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.OneWay
            });

            var popupBorder = new FrameworkElementFactory(typeof(Border), "SubmenuBorder");
            popupBorder.SetValue(Border.BackgroundProperty, raisedSurface);
            popupBorder.SetValue(Border.BorderBrushProperty, border);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(3));
            popupBorder.SetValue(FrameworkElement.MinWidthProperty, 156.0);
            popupBorder.SetValue(FrameworkElement.MaxHeightProperty, 420.0);
            popupBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var popupScroll = new FrameworkElementFactory(typeof(ScrollViewer));
            popupScroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            popupScroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            popupScroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            popupScroll.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
            var popupItems = new FrameworkElementFactory(typeof(ItemsPresenter));
            popupScroll.AppendChild(popupItems);
            popupBorder.AppendChild(popupScroll);
            popup.AppendChild(popupBorder);
            root.AppendChild(popup);

            template.VisualTree = root;
            template.Triggers.Add(new Trigger
            {
                Property = MenuItem.IsHighlightedProperty,
                Value = true,
                Setters =
                {
                    new Setter(Border.BackgroundProperty, surface, "ItemBorder"),
                    new Setter(Border.BorderBrushProperty, accentHover, "ItemBorder")
                }
            });
            template.Triggers.Add(new Trigger
            {
                Property = MenuItem.IsSubmenuOpenProperty,
                Value = true,
                Setters =
                {
                    new Setter(Border.BackgroundProperty, surface, "ItemBorder"),
                    new Setter(Border.BorderBrushProperty, accentHover, "ItemBorder")
                }
            });
            template.Triggers.Add(new Trigger
            {
                Property = MenuItem.IsCheckedProperty,
                Value = true,
                Setters = { new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckGlyph") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = MenuItem.HasItemsProperty,
                Value = true,
                Setters = { new Setter(UIElement.VisibilityProperty, Visibility.Visible, "SubmenuArrow") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = MenuItem.RoleProperty,
                Value = MenuItemRole.TopLevelHeader,
                Setters =
                {
                    new Setter(Popup.PlacementProperty, PlacementMode.Bottom, "PART_Popup"),
                    new Setter(UIElement.VisibilityProperty, Visibility.Hidden, "SubmenuArrow")
                }
            });
            template.Triggers.Add(new Trigger
            {
                Property = MenuItem.RoleProperty,
                Value = MenuItemRole.TopLevelItem,
                Setters = { new Setter(UIElement.VisibilityProperty, Visibility.Hidden, "SubmenuArrow") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false,
                Setters = { new Setter(UIElement.OpacityProperty, 0.55, "MenuItemRoot") }
            });
            return template;
        }

        private static ControlTemplate CreateSeparatorTemplate()
        {
            var template = new ControlTemplate(typeof(Separator));
            var line = new FrameworkElementFactory(typeof(Border));
            line.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            line.SetValue(Border.SnapsToDevicePixelsProperty, true);
            template.VisualTree = line;
            return template;
        }
    }
}
