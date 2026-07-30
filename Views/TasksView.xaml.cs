using System.Windows;
using System.Windows.Controls;
using FocusPanel.Models;
using FocusPanel.ViewModels;

namespace FocusPanel.Views;

public partial class TasksView : UserControl
{
    private TaskDetailWindow? _detailWindow;
    private TasksViewModel? _subscribedViewModel;

    public TasksView()
    {
        InitializeComponent();
        DataContextChanged +=
            TasksView_DataContextChanged;
        Loaded += (_, _) =>
            AttachViewModel(
                DataContext as TasksViewModel);
        Unloaded += (_, _) =>
        {
            AttachViewModel(null);
            CloseDetailWindow();
        };
    }

    private void TasksView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(
            e.NewValue as TasksViewModel);
        if (e.OldValue != e.NewValue)
            CloseDetailWindow();
    }

    private void AttachViewModel(
        TasksViewModel? viewModel)
    {
        if (ReferenceEquals(
                _subscribedViewModel,
                viewModel))
        {
            return;
        }

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel
                .OpenTaskDetailRequested -=
                OnOpenTaskDetailRequested;
            _subscribedViewModel
                .CloseTaskDetailRequested -=
                OnCloseTaskDetailRequested;
        }

        _subscribedViewModel = viewModel;
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel
                .OpenTaskDetailRequested +=
                OnOpenTaskDetailRequested;
            _subscribedViewModel
                .CloseTaskDetailRequested +=
                OnCloseTaskDetailRequested;
            if (_subscribedViewModel.SelectedTask
                is TodoItem selectedTask)
            {
                _ = Dispatcher.BeginInvoke(
                    () =>
                    {
                        if (ReferenceEquals(
                                _subscribedViewModel,
                                viewModel)
                            && IsLoaded)
                        {
                            OnOpenTaskDetailRequested(
                                selectedTask);
                        }
                    });
            }
        }
    }

    private void OnOpenTaskDetailRequested(
        TodoItem item)
    {
        if (_detailWindow is
            { IsVisible: true } existing)
        {
            existing.Activate();
            return;
        }

        var detailWindow =
            new TaskDetailWindow
            {
                DataContext =
                    DataContext
            };
        _detailWindow = detailWindow;
        detailWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(
                    _detailWindow,
                    detailWindow))
            {
                _detailWindow = null;
            }

            if (DataContext
                    is TasksViewModel viewModel
                && viewModel.SelectedTask != null)
            {
                viewModel.SelectedTask = null;
            }
        };
        detailWindow.Show();
    }

    private void OnCloseTaskDetailRequested()
        => CloseDetailWindow();

    private void CloseDetailWindow()
    {
        TaskDetailWindow? window =
            _detailWindow;
        _detailWindow = null;
        window?.Close();
    }
}
