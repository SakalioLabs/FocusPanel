using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FocusPanel.Services;

public static class FocusMenuTheme
{
    private const string TextBrushKey = "FocusTextBrush";
    private const string SurfaceBrushKey = "FocusPopupSurfaceBrush";
    private const string SelectionBrushKey = "FocusAccentSoftBrush";

    public static bool Apply(ContextMenu menu)
    {
        if (Application.Current?.TryFindResource(
                "FocusContextMenu") is not Style contextMenuStyle
            || Application.Current.TryFindResource(
                "FocusMenuItem") is not Style menuItemStyle
            || Application.Current.TryFindResource(
                "FocusMenuSeparator") is not Style separatorStyle)
        {
            return false;
        }

        menu.Style = contextMenuStyle;
        menu.ItemContainerStyle = menuItemStyle;
        menu.Resources[typeof(MenuItem)] = menuItemStyle;
        menu.Resources[typeof(Separator)] = separatorStyle;
        menu.SetResourceReference(
            Control.BackgroundProperty,
            SurfaceBrushKey);
        menu.SetResourceReference(
            Control.ForegroundProperty,
            TextBrushKey);
        ApplySystemBrushFallbacks(menu);

        foreach (object item in menu.Items)
        {
            ApplyItem(
                item,
                menuItemStyle,
                separatorStyle);
        }

        return true;
    }

    private static void ApplySystemBrushFallbacks(ContextMenu menu)
    {
        // ContextMenu uses a separate popup HWND. WPF can resolve the system
        // MenuItem template while that HWND is being created, so keep its
        // fallback palette readable as well as applying the custom template.
        if (Application.Current?.TryFindResource(
                SurfaceBrushKey) is Brush surface
            && Application.Current.TryFindResource(
                TextBrushKey) is Brush text
            && Application.Current.TryFindResource(
                SelectionBrushKey) is Brush selection)
        {
            menu.Resources[SystemColors.MenuBrushKey] = surface;
            menu.Resources[SystemColors.MenuTextBrushKey] = text;
            menu.Resources[SystemColors.ControlTextBrushKey] = text;
            menu.Resources[SystemColors.HighlightBrushKey] = selection;
            menu.Resources[SystemColors.HighlightTextBrushKey] = text;
        }
    }

    private static void ApplyItem(
        object item,
        Style menuItemStyle,
        Style separatorStyle)
    {
        switch (item)
        {
            case MenuItem menuItem:
                menuItem.Style = menuItemStyle;
                menuItem.ItemContainerStyle =
                    menuItemStyle;
                menuItem.SetResourceReference(
                    Control.ForegroundProperty,
                    TextBrushKey);
                if (menuItem.Header is TextBlock header)
                {
                    header.SetResourceReference(
                        TextBlock.ForegroundProperty,
                        TextBrushKey);
                }
                foreach (object child in menuItem.Items)
                {
                    ApplyItem(
                        child,
                        menuItemStyle,
                        separatorStyle);
                }
                break;
            case Separator separator:
                separator.Style = separatorStyle;
                break;
        }
    }
}
