using System.Windows;
using System.Windows.Controls;

namespace FocusPanel.Services;

public static class FocusMenuTheme
{
    private const string TextBrushKey = "FocusTextBrush";

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
        menu.SetResourceReference(
            Control.ForegroundProperty,
            TextBrushKey);

        foreach (object item in menu.Items)
        {
            ApplyItem(
                item,
                menuItemStyle,
                separatorStyle);
        }

        return true;
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
