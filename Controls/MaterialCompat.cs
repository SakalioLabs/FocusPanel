using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FocusPanel.Controls;

public sealed class PackIcon : TextBlock
{
    private static readonly IReadOnlyDictionary<string, string> Glyphs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ArrowLeft"] = "\uE72B",
            ["AutoFix"] = "\uE8E5",
            ["BackupRestore"] = "\uE777",
            ["CheckCircle"] = "\uE73E",
            ["ChevronRight"] = "\uE76C",
            ["Close"] = "\uE8BB",
            ["CloudSync"] = "\uE895",
            ["Cog"] = "\uE713",
            ["Delete"] = "\uE74D",
            ["DeleteOutline"] = "\uE74D",
            ["DotsVertical"] = "\uE712",
            ["FolderStarOutline"] = "\uE8B7",
            ["ImageOutline"] = "\uEB9F",
            ["OpenInNew"] = "\uE8A7",
            ["Organize"] = "\uE8CB",
            ["Pause"] = "\uE769",
            ["Play"] = "\uE768",
            ["Plus"] = "\uE710",
            ["PlusBoxOutline"] = "\uE710",
            ["Refresh"] = "\uE72C",
            ["Stop"] = "\uE71A",
            ["Target"] = "\uF272",
            ["Timer"] = "\uE916",
            ["ToolboxOutline"] = "\uEC7A",
            ["ViewModule"] = "\uECA5"
        };

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(object),
        typeof(PackIcon),
        new PropertyMetadata(null, OnKindChanged));

    public PackIcon()
    {
        FontFamily = new FontFamily("Segoe Fluent Icons");
        FontSize = 20;
        TextAlignment = TextAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    public object? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static void OnKindChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var icon = (PackIcon)dependencyObject;
        string name = args.NewValue?.ToString() ?? string.Empty;
        icon.Text = Glyphs.TryGetValue(name, out string? glyph) ? glyph : "\uE946";
    }
}

public static class ButtonAssist
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius",
        typeof(CornerRadius),
        typeof(ButtonAssist),
        new FrameworkPropertyMetadata(default(CornerRadius), FrameworkPropertyMetadataOptions.Inherits));

    public static void SetCornerRadius(DependencyObject element, CornerRadius value) =>
        element.SetValue(CornerRadiusProperty, value);

    public static CornerRadius GetCornerRadius(DependencyObject element) =>
        (CornerRadius)element.GetValue(CornerRadiusProperty);
}

public static class HintAssist
{
    public static readonly DependencyProperty HintProperty = DependencyProperty.RegisterAttached(
        "Hint",
        typeof(object),
        typeof(HintAssist),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HelperTextProperty = DependencyProperty.RegisterAttached(
        "HelperText",
        typeof(object),
        typeof(HintAssist),
        new PropertyMetadata(null));

    public static void SetHint(DependencyObject element, object value) => element.SetValue(HintProperty, value);
    public static object GetHint(DependencyObject element) => element.GetValue(HintProperty);
    public static void SetHelperText(DependencyObject element, object value) => element.SetValue(HelperTextProperty, value);
    public static object GetHelperText(DependencyObject element) => element.GetValue(HelperTextProperty);
}

public static class ShadowAssist
{
    public static readonly DependencyProperty ShadowDepthProperty = DependencyProperty.RegisterAttached(
        "ShadowDepth",
        typeof(object),
        typeof(ShadowAssist),
        new PropertyMetadata(null));

    public static void SetShadowDepth(DependencyObject element, object value) =>
        element.SetValue(ShadowDepthProperty, value);

    public static object GetShadowDepth(DependencyObject element) =>
        element.GetValue(ShadowDepthProperty);
}

public class Card : ContentControl
{
    public static readonly DependencyProperty UniformCornerRadiusProperty = DependencyProperty.Register(
        nameof(UniformCornerRadius),
        typeof(double),
        typeof(Card),
        new PropertyMetadata(16d));

    public double UniformCornerRadius
    {
        get => (double)GetValue(UniformCornerRadiusProperty);
        set => SetValue(UniformCornerRadiusProperty, value);
    }

}

public class PopupBox : ContentControl
{
    public static readonly DependencyProperty ToggleContentProperty = DependencyProperty.Register(
        nameof(ToggleContent),
        typeof(object),
        typeof(PopupBox),
        new PropertyMetadata(null));

    public static readonly DependencyProperty IsPopupOpenProperty = DependencyProperty.Register(
        nameof(IsPopupOpen),
        typeof(bool),
        typeof(PopupBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlacementModeProperty = DependencyProperty.Register(
        nameof(PlacementMode),
        typeof(PlacementMode),
        typeof(PopupBox),
        new PropertyMetadata(PlacementMode.Bottom));

    public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register(
        nameof(StaysOpen),
        typeof(bool),
        typeof(PopupBox),
        new PropertyMetadata(false));

    public object? ToggleContent
    {
        get => GetValue(ToggleContentProperty);
        set => SetValue(ToggleContentProperty, value);
    }

    public bool IsPopupOpen
    {
        get => (bool)GetValue(IsPopupOpenProperty);
        set => SetValue(IsPopupOpenProperty, value);
    }

    public PlacementMode PlacementMode
    {
        get => (PlacementMode)GetValue(PlacementModeProperty);
        set => SetValue(PlacementModeProperty, value);
    }

    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }
}
