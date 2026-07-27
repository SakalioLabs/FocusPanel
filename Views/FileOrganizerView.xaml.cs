using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FocusPanel.Helpers;
using FocusPanel.Models;
using FocusPanel.ViewModels;

namespace FocusPanel.Views;

public partial class FileOrganizerView : UserControl
{
    private readonly DispatcherTimer _autoScrollTimer;
    private double _scrollSpeed;
    private ScrollViewer? _scrollViewer;
    private bool _isDragOverOrganizer;

    public FileOrganizerView()
    {
        InitializeComponent();
        _autoScrollTimer = new DispatcherTimer();
        _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(20);
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;
    }

    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragOverOrganizer || _scrollViewer == null || _scrollSpeed == 0)
        {
            StopAutoScroll();
            return;
        }

        _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + _scrollSpeed);
    }

    private void FileCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement card && card.DataContext is DesktopFile file && DataContext is FileOrganizerViewModel vm)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                vm.ToggleFileSelection(file);
                e.Handled = true;
            }
            else
            {
                vm.SelectFileCommand.Execute(file);
            }
        }
    }

    private async void FileCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement card && card.DataContext is DesktopFile file)
        {
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
    }

    private void Partition_DragOver(object sender, DragEventArgs e)
    {
        _isDragOverOrganizer = true;
        // ... (Existing logic) ...
        
        // Auto-scroll logic
        if (_scrollViewer == null)
        {
             _scrollViewer = FindVisualChild<ScrollViewer>(this);
        }

        if (_scrollViewer != null)
        {
            Point position = e.GetPosition(_scrollViewer);
            double height = _scrollViewer.ActualHeight;
            double tolerance = 60; // Activation area height

            if (position.Y < tolerance)
            {
                _scrollSpeed = -10; // Scroll up
                if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
            }
            else if (position.Y > height - tolerance)
            {
                _scrollSpeed = 10; // Scroll down
                if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
            }
            else
            {
                _scrollSpeed = 0;
                _autoScrollTimer.Stop();
            }
        }
        
        // ... (Rest of visual feedback logic) ...
        // This is necessary to allow drop
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        
        // Visual Feedback for Insertion
        if (e.Data.GetData(typeof(PartitionViewModel)) is PartitionViewModel source && sender is Border border)
        {
             // Determine top or bottom half
             Point p = e.GetPosition(border);
             bool isBottom = p.Y > (border.ActualHeight / 2);
             
             if (isBottom)
             {
                 // Insert After (Bottom Line)
                 border.BorderBrush = (Brush)FindResource("PrimaryHueMidBrush");
                 border.BorderThickness = new Thickness(1, 1, 1, 4);
             }
             else
             {
                 // Insert Before (Top Line)
                 border.BorderBrush = (Brush)FindResource("PrimaryHueMidBrush");
                 border.BorderThickness = new Thickness(1, 4, 1, 1);
             }
        }
    }
    
    // Helper to find ScrollViewer
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            T? childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private void Partition_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = (Brush)FindResource("PrimaryHueMidBrush");
            border.Background = (Brush)FindResource("FocusSurfaceSoftBrush");
            // Keep thickness same to avoid jitter
        }
    }

    private void Partition_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            RestorePartitionChrome(border);
        }
        
        // Stop scroll if leaving the container (optional, but safer)
        // However, DragLeave fires when entering children too, so we can't blindly stop.
    }
    
    private void UserControl_DragLeave(object sender, DragEventArgs e)
    {
        StopAutoScroll();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        StopAutoScroll();
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
                            MessageBox.Show(
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
