using System;
using System.Windows;
using System.Windows.Controls;

namespace FocusPanel.Controls;

/// <summary>
/// Keeps organizer icons in a dense, width-aware grid. The final row stays
/// left aligned and a transient narrow measurement cannot permanently reduce
/// the layout to a single column.
/// </summary>
public sealed class AdaptiveIconGridPanel : Panel
{
    public static readonly DependencyProperty MinimumItemWidthProperty =
        DependencyProperty.Register(
            nameof(MinimumItemWidth),
            typeof(double),
            typeof(AdaptiveIconGridPanel),
            new FrameworkPropertyMetadata(
                100d,
                FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(
            nameof(Spacing),
            typeof(double),
            typeof(AdaptiveIconGridPanel),
            new FrameworkPropertyMetadata(
                8d,
                FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double MinimumItemWidth
    {
        get => (double)GetValue(MinimumItemWidthProperty);
        set => SetValue(MinimumItemWidthProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = ResolveWidth(availableSize.Width);
        LayoutMetrics metrics = CalculateMetrics(width);
        double rowHeight = 0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(
                new Size(metrics.ItemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        int rows = metrics.Columns == 0
            ? 0
            : (InternalChildren.Count + metrics.Columns - 1)
              / metrics.Columns;
        double height = rows == 0
            ? 0
            : rows * rowHeight + (rows - 1) * metrics.Spacing;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double width = ResolveWidth(finalSize.Width);
        LayoutMetrics metrics = CalculateMetrics(width);
        double rowHeight = InternalChildren.Count == 0
            ? 0
            : Math.Max(
                0,
                InternalChildren[0].DesiredSize.Height);
        foreach (UIElement child in InternalChildren)
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);

        for (int index = 0; index < InternalChildren.Count; index++)
        {
            int column = index % metrics.Columns;
            int row = index / metrics.Columns;
            InternalChildren[index].Arrange(
                new Rect(
                    column * (metrics.ItemWidth + metrics.Spacing),
                    row * (rowHeight + metrics.Spacing),
                    metrics.ItemWidth,
                    rowHeight));
        }

        int rowCount = metrics.Columns == 0
            ? 0
            : (InternalChildren.Count + metrics.Columns - 1)
              / metrics.Columns;
        double height = rowCount == 0
            ? 0
            : rowCount * rowHeight
              + (rowCount - 1) * metrics.Spacing;
        return new Size(finalSize.Width, height);
    }

    private LayoutMetrics CalculateMetrics(double width)
    {
        double itemMinimum = Math.Max(44, MinimumItemWidth);
        double spacing = Math.Max(0, Spacing);
        int columns = Math.Max(
            1,
            (int)Math.Floor(
                (width + spacing)
                / (itemMinimum + spacing)));
        double itemWidth = Math.Max(
            itemMinimum,
            (width - spacing * (columns - 1)) / columns);
        return new LayoutMetrics(columns, itemWidth, spacing);
    }

    private double ResolveWidth(double width)
    {
        if (!double.IsNaN(width)
            && !double.IsInfinity(width)
            && width > 0)
        {
            return width;
        }

        if (!double.IsNaN(ActualWidth)
            && !double.IsInfinity(ActualWidth)
            && ActualWidth > 0)
        {
            return ActualWidth;
        }

        return Math.Max(44, MinimumItemWidth);
    }

    private readonly record struct LayoutMetrics(
        int Columns,
        double ItemWidth,
        double Spacing);
}
