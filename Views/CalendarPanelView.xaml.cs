using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FocusPanel.Services;
using FocusPanel.ViewModels;

namespace FocusPanel.Views;

public partial class CalendarPanelView : UserControl
{
    public CalendarPanelView()
    {
        InitializeComponent();
    }

    private void CalendarPanel_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (DataContext
                is not MainViewModel viewModel
            || !TryGetNavigationAction(
                e.Key,
                Keyboard.Modifiers,
                out CalendarNavigationAction
                    action))
        {
            return;
        }

        viewModel.NavigateCalendarCommand
            .Execute(action);
        e.Handled = true;
        QueueFocusSelectedDay();
    }

    private void CalendarPanel_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            QueueFocusSelectedDay();
    }

    private void QueueFocusSelectedDay()
    {
        _ = Dispatcher.BeginInvoke(
            FocusSelectedDay,
            DispatcherPriority.Input);
    }

    private void FocusSelectedDay()
    {
        if (!IsVisible)
            return;

        CalendarDaysItems.UpdateLayout();
        for (int index = 0;
             index
             < CalendarDaysItems.Items.Count;
             index++)
        {
            if (CalendarDaysItems.Items[index]
                    is not CalendarDayItem
                    {
                        IsSelected: true
                    })
            {
                continue;
            }

            DependencyObject? container =
                CalendarDaysItems
                    .ItemContainerGenerator
                    .ContainerFromIndex(index);
            FindVisualChild<Button>(
                    container)
                ?.Focus();
            return;
        }
    }

    private static bool TryGetNavigationAction(
        Key key,
        ModifierKeys modifiers,
        out CalendarNavigationAction action)
    {
        if (key == Key.Home
            && modifiers
                == ModifierKeys.Control)
        {
            action =
                CalendarNavigationAction.Today;
            return true;
        }
        if (modifiers != ModifierKeys.None)
        {
            action = default;
            return false;
        }

        action = key switch
        {
            Key.Left =>
                CalendarNavigationAction
                    .PreviousDay,
            Key.Right =>
                CalendarNavigationAction
                    .NextDay,
            Key.Up =>
                CalendarNavigationAction
                    .PreviousWeek,
            Key.Down =>
                CalendarNavigationAction
                    .NextWeek,
            Key.PageUp =>
                CalendarNavigationAction
                    .PreviousMonth,
            Key.PageDown =>
                CalendarNavigationAction
                    .NextMonth,
            _ => default
        };
        return key
            is Key.Left
            or Key.Right
            or Key.Up
            or Key.Down
            or Key.PageUp
            or Key.PageDown;
    }

    private static T? FindVisualChild<T>(
        DependencyObject? parent)
        where T : DependencyObject
    {
        if (parent == null)
            return null;

        int count =
            VisualTreeHelper
                .GetChildrenCount(parent);
        for (int index = 0;
             index < count;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is T match)
                return match;

            T? nested =
                FindVisualChild<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
