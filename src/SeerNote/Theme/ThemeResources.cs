using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SeerNote.Domain;

namespace SeerNote.Theme
{
    /// <summary>
    /// Central semantic resources for the code-only WPF surface.
    /// Add the returned dictionary to Application.Resources once during startup.
    /// </summary>
    public static class ThemeResources
    {
        private const string PaletteStateKey = "Seer.ThemePaletteState";

        public const string CanvasBrushKey = "Seer.CanvasBrush";
        public const string SurfaceBrushKey = "Seer.SurfaceBrush";
        public const string SurfaceRaisedBrushKey = "Seer.SurfaceRaisedBrush";
        public const string InkBrushKey = "Seer.InkBrush";
        public const string MutedBrushKey = "Seer.MutedBrush";
        public const string BorderBrushKey = "Seer.BorderBrush";
        public const string AccentBrushKey = "Seer.AccentBrush";
        public const string AccentHoverBrushKey = "Seer.AccentHoverBrush";
        public const string AccentInkBrushKey = "Seer.AccentInkBrush";
        public const string GoldBrushKey = "Seer.GoldBrush";
        public const string SuccessBrushKey = "Seer.SuccessBrush";
        public const string WarningBrushKey = "Seer.WarningBrush";
        public const string DangerBrushKey = "Seer.DangerBrush";
        public const string FocusBrushKey = "Seer.FocusBrush";

        public const string UiFontFamilyKey = "Seer.UiFontFamily";
        public const string EditorFontFamilyKey = "Seer.EditorFontFamily";

        public const string SpacingSmallKey = "Seer.SpacingSmall";
        public const string SpacingMediumKey = "Seer.SpacingMedium";
        public const string SpacingLargeKey = "Seer.SpacingLarge";
        public const string CornerRadiusKey = "Seer.CornerRadius";

        public static ResourceDictionary Create(AppTheme theme = AppTheme.Graphite)
        {
            var resources = new ResourceDictionary();
            AddSemanticBrushes(resources, theme);
            AddTypography(resources);
            AddMetrics(resources);
            AddControlStyles(resources);
            return resources;
        }

        public static void ApplyTheme(ResourceDictionary resources, AppTheme theme)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            if (!Enum.IsDefined(typeof(AppTheme), theme))
            {
                throw new ArgumentOutOfRangeException(nameof(theme));
            }
            if (SystemParameters.HighContrast)
            {
                return;
            }

            var state = resources[PaletteStateKey] as ThemePaletteState;
            if (state == null)
            {
                throw new InvalidOperationException("Theme resources do not contain a palette state.");
            }
            state.Apply(GetPalette(theme));
        }

        private static void AddSemanticBrushes(ResourceDictionary resources, AppTheme theme)
        {
            if (!Enum.IsDefined(typeof(AppTheme), theme))
            {
                throw new ArgumentOutOfRangeException(nameof(theme));
            }
            if (SystemParameters.HighContrast)
            {
                resources[CanvasBrushKey] = SystemColors.WindowBrush;
                resources[SurfaceBrushKey] = SystemColors.WindowBrush;
                resources[SurfaceRaisedBrushKey] = SystemColors.ControlBrush;
                resources[InkBrushKey] = SystemColors.WindowTextBrush;
                resources[MutedBrushKey] = SystemColors.GrayTextBrush;
                resources[BorderBrushKey] = SystemColors.WindowFrameBrush;
                resources[AccentBrushKey] = SystemColors.HighlightBrush;
                resources[AccentHoverBrushKey] = SystemColors.HighlightBrush;
                resources[AccentInkBrushKey] = SystemColors.HighlightTextBrush;
                resources[GoldBrushKey] = SystemColors.HotTrackBrush;
                resources[SuccessBrushKey] = SystemColors.HighlightBrush;
                resources[WarningBrushKey] = SystemColors.HotTrackBrush;
                resources[DangerBrushKey] = SystemColors.HighlightBrush;
                resources[FocusBrushKey] = SystemColors.HighlightBrush;
                return;
            }

            ThemePalette palette = GetPalette(theme);
            var state = new ThemePaletteState(palette);
            resources[PaletteStateKey] = state;
            resources[CanvasBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Canvas));
            resources[SurfaceBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Surface));
            resources[SurfaceRaisedBrushKey] = BoundBrush(state, nameof(ThemePaletteState.SurfaceRaised));
            resources[InkBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Ink));
            resources[MutedBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Muted));
            resources[BorderBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Border));
            resources[AccentBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Accent));
            resources[AccentHoverBrushKey] = BoundBrush(state, nameof(ThemePaletteState.AccentHover));
            resources[AccentInkBrushKey] = BoundBrush(state, nameof(ThemePaletteState.AccentInk));
            resources[GoldBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Gold));
            resources[SuccessBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Success));
            resources[WarningBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Warning));
            resources[DangerBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Danger));
            resources[FocusBrushKey] = BoundBrush(state, nameof(ThemePaletteState.Focus));
        }

        private static void AddMetrics(ResourceDictionary resources)
        {
            resources[SpacingSmallKey] = 6.0;
            resources[SpacingMediumKey] = 10.0;
            resources[SpacingLargeKey] = 18.0;
            resources[CornerRadiusKey] = new CornerRadius(8.0);
        }

        private static void AddTypography(ResourceDictionary resources)
        {
            FontFamily font = AppTypography.CurrentFontFamily;
            resources[UiFontFamilyKey] = font;
            resources[EditorFontFamilyKey] = font;
        }

        private static void AddControlStyles(ResourceDictionary resources)
        {
            var focusStyle = CreateFocusVisualStyle((Brush)resources[FocusBrushKey]);
            var ink = (Brush)resources[InkBrushKey];
            var surface = (Brush)resources[SurfaceBrushKey];
            var raisedSurface = (Brush)resources[SurfaceRaisedBrushKey];
            var border = (Brush)resources[BorderBrushKey];
            var accent = (Brush)resources[AccentBrushKey];
            var accentHover = (Brush)resources[AccentHoverBrushKey];
            var accentInk = (Brush)resources[AccentInkBrushKey];
            var focus = (Brush)resources[FocusBrushKey];
            var uiFont = (FontFamily)resources[UiFontFamilyKey];

            MenuThemeResources.Add(resources, ink, (Brush)resources[MutedBrushKey], surface, raisedSurface, border, accent, accentHover, accentInk, uiFont);

            var windowStyle = new Style(typeof(Window));
            windowStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            resources[typeof(Window)] = windowStyle;

            var textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            textBoxStyle.Setters.Add(new Setter(Control.BackgroundProperty, raisedSurface));
            textBoxStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            textBoxStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
            textBoxStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            textBoxStyle.Setters.Add(new Setter(Control.FontSizeProperty, 14.5));
            textBoxStyle.Setters.Add(new Setter(Control.MinHeightProperty, 36.0));
            textBoxStyle.Setters.Add(new Setter(TextBoxBase.SelectionBrushProperty, accent));
            textBoxStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, ink));
            textBoxStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, focusStyle));
            textBoxStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateTextBoxTemplate(raisedSurface, surface, border, accentHover, focus)));
            textBoxStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsKeyboardFocusWithinProperty,
                Value = true,
                Setters = { new Setter(Control.BorderBrushProperty, focus) }
            });
            textBoxStyle.Triggers.Add(new Trigger
            {
                Property = TextBoxBase.IsReadOnlyProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, surface) }
            });
            textBoxStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false,
                Setters = { new Setter(UIElement.OpacityProperty, 0.5) }
            });
            resources[typeof(TextBox)] = textBoxStyle;

            var comboBoxItemTemplate = new ControlTemplate(typeof(ComboBoxItem));
            var comboBoxItemBorder = new FrameworkElementFactory(typeof(Border), "ItemBorder");
            comboBoxItemBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            comboBoxItemBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            comboBoxItemBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            comboBoxItemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            comboBoxItemBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var comboBoxItemContent = new FrameworkElementFactory(typeof(ContentPresenter));
            comboBoxItemContent.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            comboBoxItemContent.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            comboBoxItemContent.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(ContentControl.ContentStringFormatProperty));
            comboBoxItemContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            comboBoxItemContent.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            comboBoxItemContent.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            comboBoxItemContent.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            comboBoxItemBorder.AppendChild(comboBoxItemContent);
            comboBoxItemTemplate.VisualTree = comboBoxItemBorder;

            var comboBoxItemStyle = new Style(typeof(ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            comboBoxItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, raisedSurface));
            comboBoxItemStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            comboBoxItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            comboBoxItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 6, 9, 6)));
            comboBoxItemStyle.Setters.Add(new Setter(Control.MinHeightProperty, 32.0));
            comboBoxItemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            comboBoxItemStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            comboBoxItemStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, focusStyle));
            comboBoxItemStyle.Setters.Add(new Setter(Control.TemplateProperty, comboBoxItemTemplate));
            comboBoxItemStyle.Triggers.Add(new Trigger
            {
                Property = ComboBoxItem.IsHighlightedProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, surface), new Setter(Control.BorderBrushProperty, accentHover) }
            });
            comboBoxItemStyle.Triggers.Add(new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, accent), new Setter(Control.ForegroundProperty, accentInk), new Setter(Control.BorderBrushProperty, accent) }
            });
            comboBoxItemStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false,
                Setters = { new Setter(UIElement.OpacityProperty, 0.55) }
            });
            resources[typeof(ComboBoxItem)] = comboBoxItemStyle;

            var comboBoxStyle = new Style(typeof(ComboBox));
            comboBoxStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            comboBoxStyle.Setters.Add(new Setter(Control.BackgroundProperty, raisedSurface));
            comboBoxStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            comboBoxStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            comboBoxStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            comboBoxStyle.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
            comboBoxStyle.Setters.Add(new Setter(Control.MinHeightProperty, 34.0));
            comboBoxStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            comboBoxStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.MaxDropDownHeightProperty, 260.0));
            comboBoxStyle.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, comboBoxItemStyle));
            comboBoxStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateComboBoxTemplate(raisedSurface, border, accentHover, focus, ink)));
            resources[typeof(ComboBox)] = comboBoxStyle;

            var expanderStyle = new Style(typeof(Expander));
            expanderStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            expanderStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            expanderStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            expanderStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            resources[typeof(Expander)] = expanderStyle;

            var buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty, surface));
            buttonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            buttonStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            buttonStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 7, 12, 7)));
            buttonStyle.Setters.Add(new Setter(Control.MinHeightProperty, 34.0));
            buttonStyle.Setters.Add(new Setter(Control.FontFamilyProperty, uiFont));
            buttonStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            buttonStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            buttonStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, focusStyle));
            buttonStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateButtonTemplate(new CornerRadius(7))));
            buttonStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, raisedSurface), new Setter(Control.BorderBrushProperty, accentHover) }
            });
            buttonStyle.Triggers.Add(new Trigger
            {
                Property = ButtonBase.IsPressedProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, raisedSurface), new Setter(Control.BorderBrushProperty, accent), new Setter(UIElement.OpacityProperty, 0.92) }
            });
            buttonStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsKeyboardFocusWithinProperty,
                Value = true,
                Setters = { new Setter(Control.BorderBrushProperty, focus) }
            });
            buttonStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false,
                Setters = { new Setter(UIElement.OpacityProperty, 0.45) }
            });
            resources[typeof(Button)] = buttonStyle;

            var primaryButtonStyle = new Style(typeof(Button), buttonStyle);
            primaryButtonStyle.Setters.Add(new Setter(Control.BackgroundProperty, accent));
            primaryButtonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, accent));
            primaryButtonStyle.Setters.Add(new Setter(Control.ForegroundProperty, accentInk));
            primaryButtonStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            primaryButtonStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, accentHover), new Setter(Control.BorderBrushProperty, accentHover) }
            });
            primaryButtonStyle.Triggers.Add(new Trigger
            {
                Property = ButtonBase.IsPressedProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, accent), new Setter(Control.BorderBrushProperty, focus), new Setter(UIElement.OpacityProperty, 0.9) }
            });
            resources["Seer.PrimaryButton"] = primaryButtonStyle;

            var quietButtonStyle = new Style(typeof(Button), buttonStyle);
            quietButtonStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            quietButtonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            quietButtonStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            quietButtonStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, raisedSurface), new Setter(Control.BorderBrushProperty, border) }
            });
            quietButtonStyle.Triggers.Add(new Trigger
            {
                Property = ButtonBase.IsPressedProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, surface), new Setter(Control.BorderBrushProperty, accent), new Setter(UIElement.OpacityProperty, 0.92) }
            });
            resources["Seer.QuietButton"] = quietButtonStyle;

            var toolbarButtonStyle = new Style(typeof(Button), buttonStyle);
            toolbarButtonStyle.Setters.Add(new Setter(Control.BackgroundProperty, surface));
            toolbarButtonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            toolbarButtonStyle.Setters.Add(new Setter(Control.MinHeightProperty, 34.0));
            toolbarButtonStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            resources["Seer.ToolbarButton"] = toolbarButtonStyle;

            var navigationButtonStyle = new Style(typeof(Button), quietButtonStyle);
            navigationButtonStyle.Setters.Add(new Setter(Control.MinHeightProperty, 38.0));
            navigationButtonStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            navigationButtonStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            resources["Seer.NavigationButton"] = navigationButtonStyle;

            var dangerButtonStyle = new Style(typeof(Button), quietButtonStyle);
            dangerButtonStyle.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)resources[DangerBrushKey]));
            dangerButtonStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, raisedSurface), new Setter(Control.BorderBrushProperty, (Brush)resources[DangerBrushKey]) }
            });
            resources["Seer.DangerButton"] = dangerButtonStyle;

            var listItemStyle = new Style(typeof(ListBoxItem));
            listItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, ink));
            listItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            listItemStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            listItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            listItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(11, 8, 11, 8)));
            listItemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            listItemStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            listItemStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, focusStyle));
            listItemStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateListBoxItemTemplate(new CornerRadius(7))));
            listItemStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, surface), new Setter(Control.BorderBrushProperty, border) }
            });
            listItemStyle.Triggers.Add(new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, raisedSurface), new Setter(Control.BorderBrushProperty, accent) }
            });
            listItemStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsKeyboardFocusWithinProperty,
                Value = true,
                Setters = { new Setter(Control.BorderBrushProperty, focus) }
            });
            resources[typeof(ListBoxItem)] = listItemStyle;

            var scrollBarStyle = new Style(typeof(ScrollBar));
            scrollBarStyle.Setters.Add(new Setter(Control.BackgroundProperty, surface));
            scrollBarStyle.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)resources[MutedBrushKey]));
            scrollBarStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            scrollBarStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, 12.0));
            scrollBarStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 10.0));
            scrollBarStyle.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72));
            scrollBarStyle.Triggers.Add(new Trigger
            {
                Property = ScrollBar.OrientationProperty,
                Value = Orientation.Horizontal,
                Setters =
                {
                    new Setter(FrameworkElement.WidthProperty, Double.NaN),
                    new Setter(FrameworkElement.MinWidthProperty, 0.0),
                    new Setter(FrameworkElement.HeightProperty, 12.0),
                    new Setter(FrameworkElement.MinHeightProperty, 10.0)
                }
            });
            scrollBarStyle.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.ForegroundProperty, accentHover), new Setter(UIElement.OpacityProperty, 1.0) }
            });
            resources[typeof(ScrollBar)] = scrollBarStyle;
        }

        private static ControlTemplate CreateButtonTemplate(CornerRadius cornerRadius)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border), "ButtonBorder");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, cornerRadius);
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            content.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(ContentControl.ContentStringFormatProperty));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private static ControlTemplate CreateTextBoxTemplate(Brush raisedSurface, Brush surface, Brush borderBrush, Brush hoverBrush, Brush focusBrush)
        {
            var template = new ControlTemplate(typeof(TextBox));
            var border = new FrameworkElementFactory(typeof(Border), "FieldBorder");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var contentHost = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            contentHost.SetValue(Control.FocusableProperty, false);
            contentHost.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            contentHost.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, new TemplateBindingExtension(TextBox.HorizontalScrollBarVisibilityProperty));
            contentHost.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, new TemplateBindingExtension(TextBox.VerticalScrollBarVisibilityProperty));
            contentHost.SetValue(ScrollViewer.CanContentScrollProperty, false);
            border.AppendChild(contentHost);
            template.VisualTree = border;
            template.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Border.BorderBrushProperty, hoverBrush, "FieldBorder") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = UIElement.IsKeyboardFocusWithinProperty,
                Value = true,
                Setters = { new Setter(Border.BorderBrushProperty, focusBrush, "FieldBorder") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = TextBoxBase.IsReadOnlyProperty,
                Value = true,
                Setters = { new Setter(Border.BackgroundProperty, surface, "FieldBorder") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false,
                Setters = { new Setter(Border.BackgroundProperty, raisedSurface, "FieldBorder"), new Setter(Border.BorderBrushProperty, borderBrush, "FieldBorder") }
            });
            return template;
        }

        private static ControlTemplate CreateListBoxItemTemplate(CornerRadius cornerRadius)
        {
            var template = new ControlTemplate(typeof(ListBoxItem));
            var border = new FrameworkElementFactory(typeof(Border), "ItemBorder");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, cornerRadius);
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            content.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(ContentControl.ContentStringFormatProperty));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private static ControlTemplate CreateComboBoxTemplate(Brush raisedSurface, Brush border, Brush accentHover, Brush focus, Brush ink)
        {
            var template = new ControlTemplate(typeof(ComboBox));
            var root = new FrameworkElementFactory(typeof(Grid), "ComboRoot");

            var fieldBorder = new FrameworkElementFactory(typeof(Border), "FieldBorder");
            fieldBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            fieldBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            fieldBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            fieldBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            fieldBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);
            root.AppendChild(fieldBorder);

            var selectedContent = new FrameworkElementFactory(typeof(ContentPresenter), "ContentSite");
            selectedContent.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
            selectedContent.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
            selectedContent.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemStringFormatProperty));
            selectedContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            selectedContent.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            selectedContent.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 0, 32, 0));
            selectedContent.SetValue(UIElement.IsHitTestVisibleProperty, false);
            root.AppendChild(selectedContent);

            var arrow = new FrameworkElementFactory(typeof(TextBlock), "DropDownArrow");
            arrow.SetValue(TextBlock.TextProperty, "⌄");
            arrow.SetValue(TextBlock.ForegroundProperty, ink);
            arrow.SetValue(TextBlock.FontSizeProperty, 14.0);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 2));
            arrow.SetValue(UIElement.IsHitTestVisibleProperty, false);
            root.AppendChild(arrow);

            var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            var toggleSurface = new FrameworkElementFactory(typeof(Border));
            toggleSurface.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            toggleTemplate.VisualTree = toggleSurface;
            var toggle = new FrameworkElementFactory(typeof(ToggleButton), "DropDownToggle");
            toggle.SetValue(Control.TemplateProperty, toggleTemplate);
            toggle.SetValue(Control.FocusableProperty, false);
            toggle.SetValue(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.TwoWay
            });
            root.AppendChild(toggle);

            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.FocusableProperty, false);
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.None);
            popup.SetValue(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.OneWay
            });
            var popupBorder = new FrameworkElementFactory(typeof(Border), "PopupBorder");
            popupBorder.SetValue(Border.BackgroundProperty, raisedSurface);
            popupBorder.SetValue(Border.BorderBrushProperty, border);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(2));
            popupBorder.SetValue(FrameworkElement.MinWidthProperty, new Binding("ActualWidth")
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.OneWay
            });
            popupBorder.SetValue(FrameworkElement.MaxHeightProperty, new TemplateBindingExtension(ComboBox.MaxDropDownHeightProperty));
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroll.AppendChild(items);
            popupBorder.AppendChild(scroll);
            popup.AppendChild(popupBorder);
            root.AppendChild(popup);

            template.VisualTree = root;
            template.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.BorderBrushProperty, accentHover, "FieldBorder") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = UIElement.IsKeyboardFocusWithinProperty,
                Value = true,
                Setters = { new Setter(Control.BorderBrushProperty, focus, "FieldBorder") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = ComboBox.IsDropDownOpenProperty,
                Value = true,
                Setters = { new Setter(TextBlock.TextProperty, "⌃", "DropDownArrow") }
            });
            template.Triggers.Add(new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false,
                Setters = { new Setter(UIElement.OpacityProperty, 0.55, "ComboRoot") }
            });
            return template;
        }

        private static Style CreateFocusVisualStyle(Brush focusBrush)
        {
            var template = new ControlTemplate(typeof(Control));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BorderBrushProperty, focusBrush);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var placeholder = new FrameworkElementFactory(typeof(AdornedElementPlaceholder));
            border.AppendChild(placeholder);
            template.VisualTree = border;

            var style = new Style(typeof(Control));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static ThemePalette GetPalette(AppTheme theme)
        {
            switch (theme)
            {
                case AppTheme.Graphite:
                    return new ThemePalette(
                        "#17191C", "#202327", "#292D32", "#F2F0EA", "#A5ABB3", "#3A3F45",
                        "#62B7AE", "#7ACAC2", "#11201F", "#C8A66A", "#7CBF94", "#D7A95A", "#E47A70", "#7ACAC2");
                case AppTheme.Midnight:
                    return new ThemePalette(
                        "#0E1621", "#15202C", "#1E2B38", "#F2F5F7", "#A4B0BC", "#344759",
                        "#75AEEF", "#91C0F5", "#08131F", "#D0A15E", "#73BD96", "#D6A459", "#E88278", "#91C0F5");
                case AppTheme.Porcelain:
                    return new ThemePalette(
                        "#F3F5F9", "#FFFFFF", "#E9EDF5", "#202124", "#5B6470", "#BFC7D3",
                        "#246E69", "#1B5C57", "#FFFFFF", "#8B641F", "#2E7D50", "#8B5F17", "#B3261E", "#246E69");
                case AppTheme.Sage:
                    return new ThemePalette(
                        "#F1F5EF", "#FBFDF9", "#E3ECE4", "#202A23", "#5D6B61", "#C3D1C5",
                        "#3B7755", "#306347", "#FFFFFF", "#8D6A32", "#2F764E", "#89651C", "#B43C35", "#3B7755");
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        private static SolidColorBrush BoundBrush(ThemePaletteState state, string colorProperty)
        {
            var brush = new SolidColorBrush();
            BindingOperations.SetBinding(
                brush,
                SolidColorBrush.ColorProperty,
                new Binding(colorProperty)
                {
                    Source = state,
                    Mode = BindingMode.OneWay
                });
            return brush;
        }

        private static Color ParseColor(string hexadecimal)
        {
            return (Color)ColorConverter.ConvertFromString(hexadecimal);
        }

        private sealed class ThemePaletteState : INotifyPropertyChanged
        {
            private Color _canvas;
            private Color _surface;
            private Color _surfaceRaised;
            private Color _ink;
            private Color _muted;
            private Color _border;
            private Color _accent;
            private Color _accentHover;
            private Color _accentInk;
            private Color _gold;
            private Color _success;
            private Color _warning;
            private Color _danger;
            private Color _focus;

            public ThemePaletteState(ThemePalette palette)
            {
                Apply(palette);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public Color Canvas { get { return _canvas; } }
            public Color Surface { get { return _surface; } }
            public Color SurfaceRaised { get { return _surfaceRaised; } }
            public Color Ink { get { return _ink; } }
            public Color Muted { get { return _muted; } }
            public Color Border { get { return _border; } }
            public Color Accent { get { return _accent; } }
            public Color AccentHover { get { return _accentHover; } }
            public Color AccentInk { get { return _accentInk; } }
            public Color Gold { get { return _gold; } }
            public Color Success { get { return _success; } }
            public Color Warning { get { return _warning; } }
            public Color Danger { get { return _danger; } }
            public Color Focus { get { return _focus; } }

            public void Apply(ThemePalette palette)
            {
                Set(ref _canvas, ParseColor(palette.Canvas), nameof(Canvas));
                Set(ref _surface, ParseColor(palette.Surface), nameof(Surface));
                Set(ref _surfaceRaised, ParseColor(palette.SurfaceRaised), nameof(SurfaceRaised));
                Set(ref _ink, ParseColor(palette.Ink), nameof(Ink));
                Set(ref _muted, ParseColor(palette.Muted), nameof(Muted));
                Set(ref _border, ParseColor(palette.Border), nameof(Border));
                Set(ref _accent, ParseColor(palette.Accent), nameof(Accent));
                Set(ref _accentHover, ParseColor(palette.AccentHover), nameof(AccentHover));
                Set(ref _accentInk, ParseColor(palette.AccentInk), nameof(AccentInk));
                Set(ref _gold, ParseColor(palette.Gold), nameof(Gold));
                Set(ref _success, ParseColor(palette.Success), nameof(Success));
                Set(ref _warning, ParseColor(palette.Warning), nameof(Warning));
                Set(ref _danger, ParseColor(palette.Danger), nameof(Danger));
                Set(ref _focus, ParseColor(palette.Focus), nameof(Focus));
            }

            private void Set(ref Color field, Color value, string propertyName)
            {
                if (field == value)
                {
                    return;
                }
                field = value;
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private sealed class ThemePalette
        {
            public ThemePalette(
                string canvas,
                string surface,
                string surfaceRaised,
                string ink,
                string muted,
                string border,
                string accent,
                string accentHover,
                string accentInk,
                string gold,
                string success,
                string warning,
                string danger,
                string focus)
            {
                Canvas = canvas;
                Surface = surface;
                SurfaceRaised = surfaceRaised;
                Ink = ink;
                Muted = muted;
                Border = border;
                Accent = accent;
                AccentHover = accentHover;
                AccentInk = accentInk;
                Gold = gold;
                Success = success;
                Warning = warning;
                Danger = danger;
                Focus = focus;
            }

            public string Canvas { get; private set; }
            public string Surface { get; private set; }
            public string SurfaceRaised { get; private set; }
            public string Ink { get; private set; }
            public string Muted { get; private set; }
            public string Border { get; private set; }
            public string Accent { get; private set; }
            public string AccentHover { get; private set; }
            public string AccentInk { get; private set; }
            public string Gold { get; private set; }
            public string Success { get; private set; }
            public string Warning { get; private set; }
            public string Danger { get; private set; }
            public string Focus { get; private set; }
        }
    }
}
