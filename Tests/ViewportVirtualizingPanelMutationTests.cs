using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FocusPanel.Controls;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ViewportVirtualizingPanelMutationTests
{
    [Fact]
    public void WrappedItems_UseTheScrollViewportInsteadOfLockingToOneColumn()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var items = new ObservableCollection<string>();
                for (int index = 0; index < 12; index++)
                    items.Add($"item-{index}");

                ItemsControl control = CreateControl(items);
                control.HorizontalAlignment = HorizontalAlignment.Stretch;
                var viewer = new ScrollViewer
                {
                    Content = control,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Padding = new Thickness(14)
                };
                var window = new Window
                {
                    Content = viewer,
                    Width = 650,
                    Height = 400,
                    Left = -10000,
                    Top = -10000,
                    Opacity = 0,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                window.Show();
                window.UpdateLayout();

                ViewportVirtualizingPanel? panel =
                    FindPanel(control);
                Assert.NotNull(panel);
                Assert.True(
                    panel!.ItemsPerRow >= 5,
                    $"Expected at least five columns, got {panel.ItemsPerRow}.");
                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    [Fact]
    public void MovingItemsBetweenRealizedPartitions_DoesNotCorruptGenerator()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var source = new ObservableCollection<string>();
                var target = new ObservableCollection<string>();
                for (int index = 0; index < 80; index++)
                    source.Add($"item-{index}");

                ItemsControl sourceControl = CreateControl(source);
                ItemsControl targetControl = CreateControl(target);
                var host = new StackPanel();
                host.Children.Add(sourceControl);
                host.Children.Add(targetControl);
                var window = new Window
                {
                    Content = host,
                    Width = 340,
                    Height = 700,
                    Left = -10000,
                    Top = -10000,
                    Opacity = 0,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                window.Show();
                Layout(sourceControl);
                Layout(targetControl);

                for (int index = 0; index < 40; index++)
                {
                    string item = source[0];
                    source.RemoveAt(0);
                    target.Add(item);
                    Layout(sourceControl);
                    Layout(targetControl);
                }

                for (int index = 0; index < 20; index++)
                {
                    string item = target[^1];
                    target.RemoveAt(target.Count - 1);
                    source.Insert(0, item);
                    Layout(targetControl);
                    Layout(sourceControl);
                }

                target.Clear();
                Layout(targetControl);

                Assert.NotNull(FindPanel(sourceControl));
                Assert.NotNull(FindPanel(targetControl));
                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private static ItemsControl CreateControl(
        ObservableCollection<string> items)
    {
#pragma warning disable CS0618
        var factory = new FrameworkElementFactory(
            typeof(ViewportVirtualizingPanel));
#pragma warning restore CS0618
        factory.SetValue(
            ViewportVirtualizingPanel.ItemWidthProperty,
            80d);
        factory.SetValue(
            ViewportVirtualizingPanel.ItemHeightProperty,
            80d);
        return new ItemsControl
        {
            ItemsSource = items,
            ItemsPanel = new ItemsPanelTemplate(factory)
        };
    }

    private static void Layout(ItemsControl control)
    {
        control.ApplyTemplate();
        control.Measure(new Size(320, 320));
        control.Arrange(new Rect(0, 0, 320, 320));
        control.UpdateLayout();
    }

    private static ViewportVirtualizingPanel? FindPanel(
        DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(parent, index);
            if (child is ViewportVirtualizingPanel panel)
                return panel;
            ViewportVirtualizingPanel? nested = FindPanel(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}
