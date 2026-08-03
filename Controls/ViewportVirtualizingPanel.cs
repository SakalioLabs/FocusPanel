using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Collections.Specialized;
using FocusPanel.Services;

namespace FocusPanel.Controls;

public sealed class ViewportVirtualizingPanel :
    VirtualizingPanel
{
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(double),
            typeof(ViewportVirtualizingPanel),
            new FrameworkPropertyMetadata(
                100d,
                FrameworkPropertyMetadataOptions
                    .AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(double),
            typeof(ViewportVirtualizingPanel),
            new FrameworkPropertyMetadata(
                120d,
                FrameworkPropertyMetadataOptions
                    .AffectsMeasure));

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(double),
            typeof(ViewportVirtualizingPanel),
            new FrameworkPropertyMetadata(
                0d,
                FrameworkPropertyMetadataOptions
                    .AffectsMeasure));

    public static readonly DependencyProperty IsWrappingProperty =
        DependencyProperty.Register(
            nameof(IsWrapping),
            typeof(bool),
            typeof(ViewportVirtualizingPanel),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions
                    .AffectsMeasure));

    private ScrollViewer? _scrollOwner;
    private ViewportVirtualizationLayout? _layout;
    private double _cellWidth = 1;
    private double _cellHeight = 1;

    public ViewportVirtualizingPanel()
    {
        Loaded += Panel_Loaded;
        Unloaded += Panel_Unloaded;
    }

    public double ItemWidth
    {
        get => (double)GetValue(
            ItemWidthProperty);
        set => SetValue(
            ItemWidthProperty,
            value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(
            ItemHeightProperty);
        set => SetValue(
            ItemHeightProperty,
            value);
    }

    public double ItemSpacing
    {
        get => (double)GetValue(
            ItemSpacingProperty);
        set => SetValue(
            ItemSpacingProperty,
            value);
    }

    public bool IsWrapping
    {
        get => (bool)GetValue(
            IsWrappingProperty);
        set => SetValue(
            IsWrappingProperty,
            value);
    }

    internal int RealizedContainerCount =>
        InternalChildren.Count;

    internal int FirstRealizedIndex =>
        _layout?.FirstRealizedIndex ?? -1;

    protected override Size MeasureOverride(
        Size availableSize)
    {
        ItemsControl? owner =
            ItemsControl.GetItemsOwner(this);
        int itemCount =
            owner?.Items.Count ?? 0;
        double panelWidth = ResolvePanelWidth(
            availableSize.Width);
        double spacing = Math.Max(
            0,
            ItemSpacing);
        _cellWidth = Math.Max(
            1,
            ItemWidth + spacing);
        _cellHeight = Math.Max(
            1,
            ItemHeight + spacing);
        (double visibleTop, double visibleBottom) =
            GetVisibleRange();
        _layout =
            ViewportVirtualizationCalculator
                .Calculate(
                    itemCount,
                    panelWidth,
                    _cellWidth,
                    _cellHeight,
                    visibleTop,
                    visibleBottom,
                    IsWrapping);

        RealizeRange(
            _layout,
            panelWidth);
        return new Size(
            panelWidth,
            _layout.ExtentHeight);
    }

    protected override Size ArrangeOverride(
        Size finalSize)
    {
        if (_layout == null)
            return finalSize;

        IItemContainerGenerator generator =
            ItemContainerGenerator;
        for (int childIndex = 0;
             childIndex < InternalChildren.Count;
             childIndex++)
        {
            UIElement child =
                InternalChildren[childIndex];
            int itemIndex =
                generator.IndexFromGeneratorPosition(
                    new GeneratorPosition(
                        childIndex,
                        0));
            if (itemIndex < 0)
                continue;

            int row =
                itemIndex
                / _layout.ItemsPerRow;
            int column =
                itemIndex
                % _layout.ItemsPerRow;
            double x = IsWrapping
                ? column * _cellWidth
                : 0;
            double width = IsWrapping
                ? _cellWidth
                : finalSize.Width;
            child.Arrange(
                new Rect(
                    x,
                    row * _cellHeight,
                    width,
                    _cellHeight));
        }
        return finalSize;
    }

    protected override void OnItemsChanged(
        object sender,
        ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(
            sender,
            args);
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                RemoveStaleChildren(
                    args.Position,
                    args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Move:
                RemoveStaleChildren(
                    args.OldPosition,
                    args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Reset:
                if (InternalChildren.Count > 0)
                {
                    RemoveInternalChildRange(
                        0,
                        InternalChildren.Count);
                }
                break;
        }
        InvalidateMeasure();
    }

    private void RemoveStaleChildren(
        GeneratorPosition position,
        int count)
    {
        if (count <= 0
            || InternalChildren.Count == 0)
        {
            return;
        }

        int startIndex = Math.Max(
            0,
            position.Index);
        if (startIndex >= InternalChildren.Count)
            return;

        int safeCount = Math.Min(
            count,
            InternalChildren.Count - startIndex);
        if (safeCount > 0)
        {
            RemoveInternalChildRange(
                startIndex,
                safeCount);
        }
    }

    private void RealizeRange(
        ViewportVirtualizationLayout layout,
        double panelWidth)
    {
        CleanUpItems(layout);
        if (!layout.HasRealizedItems)
            return;

        IItemContainerGenerator generator =
            ItemContainerGenerator;
        GeneratorPosition start =
            generator.GeneratorPositionFromIndex(
                layout.FirstRealizedIndex);
        int childIndex = start.Offset == 0
            ? start.Index
            : start.Index + 1;
        using (generator.StartAt(
                   start,
                   GeneratorDirection.Forward,
                   true))
        {
            for (int itemIndex =
                     layout.FirstRealizedIndex;
                 itemIndex
                     <= layout.LastRealizedIndex;
                 itemIndex++, childIndex++)
            {
                bool isNewlyRealized;
                var child =
                    (UIElement)generator.GenerateNext(
                        out isNewlyRealized);
                bool isAlreadyAtPosition =
                    childIndex
                        < InternalChildren.Count
                    && ReferenceEquals(
                        InternalChildren[childIndex],
                        child);
                if (!isAlreadyAtPosition)
                {
                    if (childIndex
                        >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(
                            childIndex,
                            child);
                    }
                }
                if (isNewlyRealized
                    || !isAlreadyAtPosition)
                {
                    generator.PrepareItemContainer(
                        child);
                }

                child.Measure(
                    new Size(
                        IsWrapping
                            ? _cellWidth
                            : panelWidth,
                        _cellHeight));
            }
        }
    }

    private void CleanUpItems(
        ViewportVirtualizationLayout layout)
    {
        IItemContainerGenerator generator =
            ItemContainerGenerator;
        for (int childIndex =
                 InternalChildren.Count - 1;
             childIndex >= 0;
             childIndex--)
        {
            GeneratorPosition position =
                new(childIndex, 0);
            int itemIndex =
                generator.IndexFromGeneratorPosition(
                    position);
            if (itemIndex < 0)
            {
                // ItemContainerGenerator already discarded this slot after
                // a source collection mutation. Calling Remove/Recycle with
                // the stale position enters WPF with an invalid generator
                // node and can throw from inside ItemContainerGenerator.
                RemoveInternalChildRange(
                    childIndex,
                    1);
                continue;
            }
            if (layout.HasRealizedItems
                && itemIndex
                    >= layout.FirstRealizedIndex
                && itemIndex
                    <= layout.LastRealizedIndex)
            {
                continue;
            }

            if (generator
                is IRecyclingItemContainerGenerator
                    recycling)
            {
                recycling.Recycle(position, 1);
            }
            else
            {
                generator.Remove(position, 1);
            }
            RemoveInternalChildRange(
                childIndex,
                1);
        }
    }

    private (double Top, double Bottom)
        GetVisibleRange()
    {
        AttachScrollOwner();
        if (_scrollOwner == null)
        {
            return (
                0,
                Math.Max(
                    _cellHeight * 3,
                    320));
        }
        if (_scrollOwner.ViewportHeight <= 0)
            return (0, 0);

        try
        {
            GeneralTransform transform =
                TransformToAncestor(
                    _scrollOwner);
            double top = transform
                .Transform(new Point(0, 0))
                .Y;
            return (
                -top,
                _scrollOwner.ViewportHeight
                    - top);
        }
        catch (InvalidOperationException)
        {
            return (
                0,
                _scrollOwner.ViewportHeight);
        }
    }

    private double ResolvePanelWidth(
        double availableWidth)
    {
        if (!double.IsNaN(availableWidth)
            && !double.IsInfinity(availableWidth)
            && availableWidth > 0)
        {
            return availableWidth;
        }

        if (ActualWidth > 0)
            return ActualWidth;
        if (VisualTreeHelper.GetParent(this)
            is FrameworkElement parent
            && parent.ActualWidth > 0)
        {
            return parent.ActualWidth;
        }
        return Math.Max(
            1,
            ItemWidth + ItemSpacing);
    }

    private void Panel_Loaded(
        object sender,
        RoutedEventArgs e)
        => AttachScrollOwner();

    private void Panel_Unloaded(
        object sender,
        RoutedEventArgs e)
        => DetachScrollOwner();

    private void AttachScrollOwner()
    {
        ScrollViewer? owner =
            FindScrollViewer();
        if (ReferenceEquals(
                owner,
                _scrollOwner))
        {
            return;
        }

        DetachScrollOwner();
        _scrollOwner = owner;
        if (_scrollOwner == null)
            return;

        _scrollOwner.ScrollChanged +=
            ScrollOwner_ScrollChanged;
        _scrollOwner.SizeChanged +=
            ScrollOwner_SizeChanged;
    }

    private void DetachScrollOwner()
    {
        if (_scrollOwner == null)
            return;

        _scrollOwner.ScrollChanged -=
            ScrollOwner_ScrollChanged;
        _scrollOwner.SizeChanged -=
            ScrollOwner_SizeChanged;
        _scrollOwner = null;
    }

    private ScrollViewer? FindScrollViewer()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            current =
                VisualTreeHelper.GetParent(current);
            if (current is ScrollViewer viewer)
                return viewer;
        }
        return null;
    }

    private void ScrollOwner_ScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0
            || e.ViewportHeightChange != 0
            || e.ExtentHeightChange != 0)
        {
            InvalidateMeasure();
        }
    }

    private void ScrollOwner_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
        => InvalidateMeasure();
}
