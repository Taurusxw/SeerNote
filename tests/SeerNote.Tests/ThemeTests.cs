using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SeerNote.Domain;
using SeerNote.Theme;

namespace SeerNote.Tests
{
    public static class ThemeTests
    {
        public static void RunAll()
        {
            BundledTypographySupportsMixedChineseAndLatin();
            ThemeSwitchesMutateStableSemanticBrushes();
            MenusOwnEveryNestedSurface();
            ControlGrammarUsesStableSemanticTemplates();
            FocusStatesKeepControlGeometryStable();
        }

        private static void BundledTypographySupportsMixedChineseAndLatin()
        {
            Require(File.Exists(AppTypography.BundledFontPath), "The portable distribution should stage its private font beside the application assembly.");
            Require(AppTypography.IsBundledFontAvailable, "The private Source Han Sans CN family should load without a Windows font installation: " + (AppTypography.LoadError == null ? String.Empty : AppTypography.LoadError.Message));

            var typeface = new Typeface(
                AppTypography.CurrentFontFamily,
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);
            GlyphTypeface glyphTypeface;
            Require(typeface.TryGetGlyphTypeface(out glyphTypeface), "The private app font should resolve to an OpenType glyph face.");
            const string sample = "SeerNote 中文记录 Prompt 123";
            foreach (char character in sample.Where(character => !Char.IsWhiteSpace(character)))
            {
                Require(glyphTypeface.CharacterToGlyphMap.ContainsKey(character), "The private font is missing a required mixed-language glyph: " + character);
            }
            Require(glyphTypeface.EmbeddingRights == FontEmbeddingRight.Installable, "The bundled font should report installable embedding rights.");

            ResourceDictionary resources = ThemeResources.Create(AppTheme.Porcelain);
            FontFamily uiFont = resources[ThemeResources.UiFontFamilyKey] as FontFamily;
            FontFamily editorFont = resources[ThemeResources.EditorFontFamilyKey] as FontFamily;
            Require(Object.ReferenceEquals(uiFont, AppTypography.CurrentFontFamily) && Object.ReferenceEquals(editorFont, uiFont), "UI and editor roles should share the verified app-private mixed-language family.");
            Require(Object.ReferenceEquals(SetterValue<FontFamily>((Style)resources[typeof(TextBox)], Control.FontFamilyProperty), uiFont), "Text editors should inherit the private font through the central theme boundary.");
            Require(Object.ReferenceEquals(SetterValue<FontFamily>((Style)resources[typeof(Button)], Control.FontFamilyProperty), uiFont), "Buttons should use the same mixed-language metrics as the rest of the UI.");
        }

        private static void ThemeSwitchesMutateStableSemanticBrushes()
        {
            if (SystemParameters.HighContrast)
            {
                return;
            }

            Array themes = Enum.GetValues(typeof(AppTheme));
            Require(themes.Length == 4, "SeerNote should intentionally expose exactly four themes.");

            ResourceDictionary themeResources = ThemeResources.Create(AppTheme.Graphite);
            var applicationResources = new ResourceDictionary();
            applicationResources.MergedDictionaries.Add(themeResources);
            string[] brushKeys =
            {
                ThemeResources.CanvasBrushKey,
                ThemeResources.SurfaceBrushKey,
                ThemeResources.SurfaceRaisedBrushKey,
                ThemeResources.InkBrushKey,
                ThemeResources.MutedBrushKey,
                ThemeResources.BorderBrushKey,
                ThemeResources.AccentBrushKey,
                ThemeResources.AccentHoverBrushKey,
                ThemeResources.AccentInkBrushKey,
                ThemeResources.GoldBrushKey,
                ThemeResources.SuccessBrushKey,
                ThemeResources.WarningBrushKey,
                ThemeResources.DangerBrushKey,
                ThemeResources.FocusBrushKey
            };
            var originalBrushes = new SolidColorBrush[brushKeys.Length];
            for (int index = 0; index < brushKeys.Length; index++)
            {
                originalBrushes[index] = Brush(applicationResources, brushKeys[index]);
                Require(
                    BindingOperations.GetBindingExpression(originalBrushes[index], SolidColorBrush.ColorProperty) != null,
                    brushKeys[index] + " must bind its color to the theme palette state.");
            }
            SolidColorBrush originalCanvas = originalBrushes[0];

            var styledTextBox = new TextBox
            {
                Style = (Style)applicationResources[typeof(TextBox)]
            };
            Require(styledTextBox.Style.IsSealed, "Applying a theme style should reproduce WPF style sealing.");
            foreach (string brushKey in brushKeys)
            {
                Require(!Brush(applicationResources, brushKey).CanFreeze, brushKey + " must remain non-freezable after entering WPF resources.");
            }

            Color graphiteCanvas = originalCanvas.Color;
            ThemeResources.ApplyTheme(applicationResources, AppTheme.Porcelain);
            Require(originalCanvas.Color != graphiteCanvas, "Theme switching must update the bound brush color.");

            foreach (AppTheme theme in themes)
            {
                ThemeResources.ApplyTheme(applicationResources, theme);
                for (int index = 0; index < brushKeys.Length; index++)
                {
                    Require(Object.ReferenceEquals(originalBrushes[index], Brush(applicationResources, brushKeys[index])), "Theme switching must preserve brush identity: " + brushKeys[index]);
                }
                Require(Contrast(Brush(applicationResources, ThemeResources.InkBrushKey).Color, Brush(applicationResources, ThemeResources.CanvasBrushKey).Color) >= 4.5, theme + " ink/canvas contrast is below 4.5:1.");
                Require(Contrast(Brush(applicationResources, ThemeResources.InkBrushKey).Color, Brush(applicationResources, ThemeResources.SurfaceBrushKey).Color) >= 4.5, theme + " ink/surface contrast is below 4.5:1.");
                Require(Contrast(Brush(applicationResources, ThemeResources.MutedBrushKey).Color, Brush(applicationResources, ThemeResources.CanvasBrushKey).Color) >= 4.5, theme + " muted/canvas contrast is below 4.5:1.");
                Require(Contrast(Brush(applicationResources, ThemeResources.AccentBrushKey).Color, Brush(applicationResources, ThemeResources.CanvasBrushKey).Color) >= 4.5, theme + " accent/canvas contrast is below 4.5:1.");
                Require(Contrast(Brush(applicationResources, ThemeResources.AccentInkBrushKey).Color, Brush(applicationResources, ThemeResources.AccentBrushKey).Color) >= 4.5, theme + " primary button contrast is below 4.5:1.");
                Require(Contrast(Brush(applicationResources, ThemeResources.GoldBrushKey).Color, Brush(applicationResources, ThemeResources.SurfaceBrushKey).Color) >= 4.5, theme + " gold/surface contrast is below 4.5:1.");
                Require(Contrast(Brush(applicationResources, ThemeResources.DangerBrushKey).Color, Brush(applicationResources, ThemeResources.SurfaceBrushKey).Color) >= 4.5, theme + " danger/surface contrast is below 4.5:1.");
                Require(Contrast(Brush(applicationResources, ThemeResources.FocusBrushKey).Color, Brush(applicationResources, ThemeResources.SurfaceBrushKey).Color) >= 3.0, theme + " focus/surface contrast is below 3:1.");
            }

            ThemeResources.ApplyTheme(applicationResources, AppTheme.Porcelain);
            Color porcelainCanvas = Brush(applicationResources, ThemeResources.CanvasBrushKey).Color;
            Color porcelainSurface = Brush(applicationResources, ThemeResources.SurfaceBrushKey).Color;
            Color porcelainRaised = Brush(applicationResources, ThemeResources.SurfaceRaisedBrushKey).Color;
            Require(porcelainSurface == Colors.White, "The Win11-inspired light theme should retain a white editing surface.");
            Require(porcelainCanvas != porcelainSurface && porcelainRaised != porcelainSurface, "The light shell, toolbar, and editing paper should remain visually distinct.");
            Require(Luminance(porcelainSurface) > Luminance(porcelainCanvas) && Luminance(porcelainSurface) > Luminance(porcelainRaised), "The editing paper should be the brightest layer in the light theme.");
        }

        private static void MenusOwnEveryNestedSurface()
        {
            ResourceDictionary resources = ThemeResources.Create(AppTheme.Graphite);
            var contextStyle = resources[typeof(ContextMenu)] as Style;
            var itemStyle = resources[typeof(MenuItem)] as Style;
            var menuStyle = resources[typeof(Menu)] as Style;
            var separatorStyle = resources[typeof(Separator)] as Style;
            Require(contextStyle != null && itemStyle != null && menuStyle != null && separatorStyle != null, "Every WPF menu primitive should have one implicit application style.");
            Require(resources[SystemColors.MenuBrushKey] != null && resources[SystemColors.MenuBarBrushKey] != null, "System-backed menu popup surfaces should be redirected to semantic resources.");
            Require(SetterValue<Style>(contextStyle, ItemsControl.ItemContainerStyleProperty) == null, "Context menus must let WPF select an implicit style for each real container type.");
            Require(SetterValue<Style>(menuStyle, ItemsControl.ItemContainerStyleProperty) == null, "Menu bars must not force the MenuItem style onto separators.");
            var contextSelector = SetterValue<StyleSelector>(contextStyle, ItemsControl.ItemContainerStyleSelectorProperty);
            var menuSelector = SetterValue<StyleSelector>(menuStyle, ItemsControl.ItemContainerStyleSelectorProperty);
            var nestedSelector = SetterValue<StyleSelector>(itemStyle, ItemsControl.ItemContainerStyleSelectorProperty);
            Require(contextSelector != null, "Context menus should select styles by the real container type.");
            Require(menuSelector != null, "Menu bars should select styles by the real container type.");
            Require(nestedSelector != null, "Nested menu items should reuse the type-aware selector at every submenu level.");

            if (Application.Current == null)
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            }
            Application.Current.Resources.MergedDictionaries.Add(resources);

            var menu = new ContextMenu();
            var move = new MenuItem { Header = "移动到分类" };
            var category = new MenuItem { Header = "未分类", IsCheckable = true, IsChecked = true };
            move.Items.Add(category);
            menu.Items.Add(move);
            menu.Items.Add(new Separator());

            Require(menu.ApplyTemplate(), "Context menu template should be applicable without showing a window.");
            menu.Measure(new Size(320, 400));
            menu.Arrange(new Rect(0, 0, 320, 400));
            menu.UpdateLayout();
            Require(Object.ReferenceEquals(menu.Style, contextStyle), "Context menus should inherit the unified implicit style from application resources.");
            Require(Object.ReferenceEquals(move.Style, itemStyle), "Menu items should still inherit the unified implicit style.");
            Require(Object.ReferenceEquals(((Separator)menu.Items[1]).Style, separatorStyle), "Separators should inherit their own implicit style instead of the MenuItem style.");
            Require(Object.ReferenceEquals(nestedSelector.SelectStyle(category, category), itemStyle), "Nested menu items should resolve to the MenuItem style.");
            Require(Object.ReferenceEquals(nestedSelector.SelectStyle(new Separator(), new Separator()), separatorStyle), "Nested separators should resolve to the Separator style.");
            move.ApplyTemplate();
            category.Style = nestedSelector.SelectStyle(category, category);
            Require(category.ApplyTemplate(), "Checked submenu item template should be applicable without showing a window.");
            var rootBorder = menu.Template.FindName("MenuBorder", menu) as Border;
            var submenuBorder = move.Template.FindName("SubmenuBorder", move) as Border;
            var check = category.Template.FindName("CheckGlyph", category) as TextBlock;
            Require(rootBorder != null && submenuBorder != null, "Root and nested popup surfaces must be owned by the Seer menu templates.");
            var submenuBackground = submenuBorder.Background as SolidColorBrush;
            Require(submenuBackground != null && submenuBackground.Color == Brush(resources, ThemeResources.SurfaceRaisedBrushKey).Color, "Nested popup background must use the semantic raised surface instead of the system light menu brush.");
            Require(check != null && check.Visibility == Visibility.Visible, "Checked submenu items should expose a visible themed check glyph.");
        }

        private static void FocusStatesKeepControlGeometryStable()
        {
            ResourceDictionary resources = ThemeResources.Create(AppTheme.Graphite);
            Type[] styledControls = { typeof(TextBox), typeof(Button), typeof(ListBoxItem) };
            foreach (Type controlType in styledControls)
            {
                var style = resources[controlType] as Style;
                Require(style != null, "Missing implicit style for " + controlType.Name + ".");
                Require(!ChangesBorderThickness(style.Triggers), controlType.Name + " focus/selection triggers must not change layout geometry.");
            }

            string[] buttonStyleKeys = { "Seer.PrimaryButton", "Seer.QuietButton", "Seer.ToolbarButton", "Seer.NavigationButton", "Seer.DangerButton" };
            foreach (string key in buttonStyleKeys)
            {
                var roleStyle = resources[key] as Style;
                Require(roleStyle != null && !ChangesBorderThickness(roleStyle.Triggers), key + " focus, hover, and pressed states must not change button dimensions.");
            }
            var comboStyle = resources[typeof(ComboBox)] as Style;
            ControlTemplate comboTemplate = SetterValue<ControlTemplate>(comboStyle, Control.TemplateProperty);
            Require(comboTemplate != null && !ChangesBorderThickness(comboTemplate.Triggers), "ComboBox focus must not change field geometry.");
        }

        private static void ControlGrammarUsesStableSemanticTemplates()
        {
            ResourceDictionary resources = ThemeResources.Create(AppTheme.Graphite);
            var buttonStyle = resources[typeof(Button)] as Style;
            var textBoxStyle = resources[typeof(TextBox)] as Style;
            var listItemStyle = resources[typeof(ListBoxItem)] as Style;
            Require(buttonStyle != null && textBoxStyle != null && listItemStyle != null, "Buttons, fields, and collection rows should all have implicit semantic styles.");

            ControlTemplate buttonTemplate = SetterValue<ControlTemplate>(buttonStyle, Control.TemplateProperty);
            ControlTemplate textBoxTemplate = SetterValue<ControlTemplate>(textBoxStyle, Control.TemplateProperty);
            ControlTemplate listItemTemplate = SetterValue<ControlTemplate>(listItemStyle, Control.TemplateProperty);
            Require(buttonTemplate != null && textBoxTemplate != null && listItemTemplate != null, "Core controls should own their rendered shape instead of leaking the operating-system default chrome.");

            var button = new Button { Style = buttonStyle, Content = "确认" };
            Require(button.ApplyTemplate(), "The semantic button template should be applicable.");
            var buttonBorder = button.Template.FindName("ButtonBorder", button) as Border;
            Require(buttonBorder != null && buttonBorder.CornerRadius.TopLeft >= 6.0, "Buttons should render with the restrained shared corner radius.");

            var field = new TextBox { Style = textBoxStyle, Text = "搜索 Note" };
            Require(field.ApplyTemplate(), "The semantic text field template should be applicable.");
            Require(field.Template.FindName("PART_ContentHost", field) is ScrollViewer, "Text fields must preserve the native WPF content host for keyboard, IME, and scrolling behavior.");

            var scrollStyle = resources[typeof(ScrollBar)] as Style;
            Require(scrollStyle != null, "Scrollable surfaces should inherit a restrained semantic scrollbar style.");
            Require(resources["Seer.ToolbarButton"] is Style && resources["Seer.NavigationButton"] is Style && resources["Seer.DangerButton"] is Style, "The workbench should expose toolbar, navigation, and danger button roles in the central theme boundary.");
        }

        private static bool ChangesBorderThickness(TriggerCollection triggers)
        {
            foreach (TriggerBase triggerBase in triggers)
            {
                var trigger = triggerBase as Trigger;
                if (trigger == null)
                {
                    continue;
                }
                foreach (SetterBase setterBase in trigger.Setters)
                {
                    var setter = setterBase as Setter;
                    if (setter != null && setter.Property == Control.BorderThicknessProperty)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static T SetterValue<T>(Style style, DependencyProperty property) where T : class
        {
            Require(style != null, "Style is required.");
            foreach (SetterBase setterBase in style.Setters)
            {
                var setter = setterBase as Setter;
                if (setter != null && setter.Property == property)
                {
                    return setter.Value as T;
                }
            }
            return null;
        }

        private static SolidColorBrush Brush(ResourceDictionary resources, string key)
        {
            var brush = resources[key] as SolidColorBrush;
            Require(brush != null, "Missing solid theme brush: " + key);
            return brush;
        }

        private static double Contrast(Color first, Color second)
        {
            double lighter = Math.Max(Luminance(first), Luminance(second));
            double darker = Math.Min(Luminance(first), Luminance(second));
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double Luminance(Color color)
        {
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }

        private static double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
