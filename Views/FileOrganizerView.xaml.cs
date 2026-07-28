using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FocusPanel.Helpers;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.ViewModels;

namespace FocusPanel.Views;

public partial class FileOrganizerView : UserControl
{
    private readonly DispatcherTimer _autoScrollTimer;
    private double _scrollSpeed;
    private bool _isDragOverOrganizer;
    private Point? _fileDragStartPoint;
    private DesktopFile? _fileDragCandidate;
    private int _transientInteractionDepth;
    private MainWindow? _transientInteractionOwner;

    public FileOrganizerView()
    {
        InitializeComponent();
        _autoScrollTimer = new DispatcherTimer();
        _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(20);
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;
    }

    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragOverOrganizer || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            StopAutoScroll();
            return;
        }

        Point pointer = Mouse.GetPosition(OrganizerScrollViewer);
        _scrollSpeed = OrganizerDragInteractionPolicy.GetAutoScrollStep(
            pointer.Y,
            OrganizerScrollViewer.ViewportHeight,
            OrganizerScrollViewer.VerticalOffset,
            OrganizerScrollViewer.ScrollableHeight);
        if (_scrollSpeed == 0)
        {
            _autoScrollTimer.Stop();
            return;
        }

        OrganizerScrollViewer.ScrollToVerticalOffset(
            OrganizerScrollViewer.VerticalOffset + _scrollSpeed);
    }

    private void FileCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement card && card.DataContext is DesktopFile file && DataContext is FileOrganizerViewModel vm)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                ClearFileDragCandidate();
                vm.ToggleFileSelection(file);
                e.Handled = true;
            }
            else
            {
                _fileDragCandidate = file;
                _fileDragStartPoint = e.GetPosition(this);
                vm.SelectFileCommand.Execute(file);
            }
        }
    }

    private async void FileCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _fileDragStartPoint is not Point dragStart
            || sender is not FrameworkElement card
            || card.DataContext is not DesktopFile file
            || !ReferenceEquals(file, _fileDragCandidate))
        {
            return;
        }

        Point current = e.GetPosition(this);
        if (!OrganizerDragInteractionPolicy.HasExceededDragThreshold(
                dragStart.X,
                dragStart.Y,
                current.X,
                current.Y,
                SystemParameters.MinimumHorizontalDragDistance,
                SystemParameters.MinimumVerticalDragDistance))
        {
            return;
        }

        ClearFileDragCandidate();
        if (DataContext is FileOrganizerViewModel vm)
        {
            vm.SelectedFile = file;
            var data = new DataObject();
            data.SetData(typeof(DesktopFile), file);
            var shell = Window.GetWindow(this) as MainWindow;
            shell?.BeginDesktopFileDrag();
            try
            {
                DragDrop.DoDragDrop(card, data, DragDropEffects.Move);

                if (file.IsHidden && DesktopHelper.IsCursorOverDesktop())
                {
                    await vm.RestoreDraggedFileToDesktop(file);
                }
            }
            finally
            {
                StopAutoScroll();
                shell?.EndDesktopFileDrag();
            }
        }
    }

    private void Partition_DragOver(object sender, DragEventArgs e)
    {
        if (!IsSupportedOrganizerDrag(e.Data))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        if (e.Data.GetData(typeof(PartitionViewModel)) is PartitionViewModel source && sender is Border border)
        {
             Point p = e.GetPosition(border);
             bool isBottom = p.Y > (border.ActualHeight / 2);
             
             if (isBottom)
             {
                 // Insert After (Bottom Line)
                 border.BorderBrush = (Brush)FindResource("FocusAccentBrush");
                 border.BorderThickness = new Thickness(1, 1, 1, 4);
             }
             else
             {
                 // Insert Before (Top Line)
                 border.BorderBrush = (Brush)FindResource("FocusAccentBrush");
                 border.BorderThickness = new Thickness(1, 4, 1, 1);
             }
        }
    }
    
    private void Organizer_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!IsSupportedOrganizerDrag(e.Data))
        {
            StopAutoScroll();
            return;
        }

        _isDragOverOrganizer = true;
        Point pointer = e.GetPosition(OrganizerScrollViewer);
        UpdateAutoScroll(pointer.Y);
    }

    private void Organizer_PreviewDragLeave(object sender, DragEventArgs e)
    {
        Point pointer = e.GetPosition(this);
        if (pointer.X < 0
            || pointer.X >= ActualWidth
            || pointer.Y < 0
            || pointer.Y >= ActualHeight)
        {
            StopAutoScroll();
        }
    }

    private void Organizer_PreviewDrop(object sender, DragEventArgs e)
        => StopAutoScroll();

    private void FileDrag_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
        => ClearFileDragCandidate();

    private void UpdateAutoScroll(double pointerY)
    {
        _scrollSpeed = OrganizerDragInteractionPolicy.GetAutoScrollStep(
            pointerY,
            OrganizerScrollViewer.ViewportHeight,
            OrganizerScrollViewer.VerticalOffset,
            OrganizerScrollViewer.ScrollableHeight);
        if (_scrollSpeed == 0)
        {
            _autoScrollTimer.Stop();
        }
        else if (!_autoScrollTimer.IsEnabled)
        {
            _autoScrollTimer.Start();
        }
    }

    private static bool IsSupportedOrganizerDrag(IDataObject data)
        => data.GetDataPresent(typeof(DesktopFile))
            || data.GetDataPresent(typeof(PartitionViewModel))
            || data.GetDataPresent(DataFormats.FileDrop);

    private void ClearFileDragCandidate()
    {
        _fileDragCandidate = null;
        _fileDragStartPoint = null;
    }

    private void Partition_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = (Brush)FindResource("FocusAccentBrush");
            border.Background = (Brush)FindResource("FocusSurfaceSoftBrush");
            // Keep thickness same to avoid jitter
        }
    }

    private void Partition_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            RestorePartitionChrome(border);
    }
    
    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ClearFileDragCandidate();
        StopAutoScroll();
        ReleaseTransientInteractions();
    }

    private void TransientSurface_Opened(object sender, RoutedEventArgs e)
        => BeginTransientSurface();

    private void TransientSurface_Closed(object sender, RoutedEventArgs e)
        => EndTransientSurface();

    private void TransientPopup_Opened(object? sender, EventArgs e)
        => BeginTransientSurface();

    private void TransientPopup_Closed(object? sender, EventArgs e)
        => EndTransientSurface();

    private void BeginTransientSurface()
    {
        _transientInteractionOwner ??= Window.GetWindow(this) as MainWindow;
        _transientInteractionDepth++;
        _transientInteractionOwner?.BeginTransientInteraction();
    }

    private void EndTransientSurface()
    {
        if (_transientInteractionDepth == 0)
            return;

        _transientInteractionDepth--;
        _transientInteractionOwner?.EndTransientInteraction();
        if (_transientInteractionDepth == 0)
            _transientInteractionOwner = null;
    }

    private void ReleaseTransientInteractions()
    {
        while (_transientInteractionDepth > 0)
        {
            _transientInteractionDepth--;
            _transientInteractionOwner?.EndTransientInteraction();
        }
        _transientInteractionOwner = null;
    }

    private async void Partition_Drop(object sender, DragEventArgs e)
    {
        StopAutoScroll();
        if (sender is Border border)
        {
            e.Handled = true;
            // Capture drop position BEFORE clearing style
            Point p = e.GetPosition(border);
            bool isBottom = p.Y > (border.ActualHeight / 2);

            RestorePartitionChrome(border);

            // Debug
            System.Diagnostics.Debug.WriteLine("Partition_Drop Fired");

            if (border.DataContext is PartitionViewModel partition && DataContext is FileOrganizerViewModel vm)
            {
                // Case 1: Internal File Drop — hide from desktop into partition
                if (e.Data.GetData(typeof(DesktopFile)) is DesktopFile file)
                {
                    vm.SelectedFile = file;
                    if (vm.HideFileToPanelCommand.CanExecute(partition.Name))
                    {
                        vm.HideFileToPanelCommand.Execute(partition.Name);
                    }
                }
                // Case 2: External File Drop (from Explorer)
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
                    {
                        var result = await vm.ImportFiles(files, partition.Name);
                        if (result.HasIssues)
                        {
                            var details = new List<string>();
                            if (result.OutsideDesktop > 0)
                                details.Add($"{result.OutsideDesktop} 个项目不在桌面根目录");
                            if (result.AuthorizationCanceled > 0)
                                details.Add($"{result.AuthorizationCanceled} 个公共桌面项目未获得管理员授权");
                            if (result.Failed > 0)
                                details.Add($"{result.Failed} 个项目写入属性失败");
                            FocusDialogService.Show(
                                $"已收纳 {result.Collected} 个桌面项目。\n{string.Join("；", details)}。\nFocusPanel 没有移动任何文件。",
                                "FocusPanel",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
                // Case 3: Partition Reordering (Dropped ONTO another partition)
                else if (e.Data.GetData(typeof(PartitionViewModel)) is PartitionViewModel sourcePartition)
                {
                     if (sourcePartition != partition)
                     {
                         // Pass the insertAfter flag
                         vm.ReorderPartition(sourcePartition, partition, isBottom);
                     }
                }
            }
        }
    }

    private void RestorePartitionChrome(Border border)
    {
        border.BorderBrush = (Brush)FindResource("FocusStrokeBrush");
        border.BorderThickness = new Thickness(1);
        border.Background = (Brush)FindResource("FocusSurfaceStrongBrush");
    }

    // Partition Reordering
    private Point _partitionDragStartPoint;

    private void PartitionHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _partitionDragStartPoint = e.GetPosition(null);
    }

    private void PartitionHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _partitionDragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _partitionDragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is FrameworkElement element && element.DataContext is PartitionViewModel partition)
                {
                    // Start Drag
                    DragDrop.DoDragDrop(element, new DataObject(typeof(PartitionViewModel), partition), DragDropEffects.Move);
                }
            }
        }
    }

    private void PartitionHeader_Drop(object sender, DragEventArgs e)
    {
        StopAutoScroll();
        if (sender is FrameworkElement element && element.DataContext is PartitionViewModel targetPartition &&
            DataContext is FileOrganizerViewModel vm &&
            e.Data.GetData(typeof(PartitionViewModel)) is PartitionViewModel sourcePartition)
        {
            e.Handled = true; // Mark as handled so it doesn't bubble up to Partition_Drop if they overlap
            if (sourcePartition != targetPartition)
            {
                vm.ReorderPartition(sourcePartition, targetPartition);
            }
        }
    }

    private void ContentArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Deselect all when clicking empty space
        if (DataContext is FileOrganizerViewModel vm)
        {
            vm.DeselectAllFiles();
        }
    }

    private void Column_Drop(object sender, DragEventArgs e)
    {
        StopAutoScroll();
        if (sender is Border border && border.Tag is string colStr && int.TryParse(colStr, out int targetColumn) &&
            DataContext is FileOrganizerViewModel vm &&
            e.Data.GetData(typeof(PartitionViewModel)) is PartitionViewModel sourcePartition)
        {
            e.Handled = true;
            // Move to end of target column
            vm.MovePartitionToColumn(sourcePartition, targetColumn);
        }
    }

    private void StopAutoScroll()
    {
        _isDragOverOrganizer = false;
        _scrollSpeed = 0;
        _autoScrollTimer.Stop();
    }
}
