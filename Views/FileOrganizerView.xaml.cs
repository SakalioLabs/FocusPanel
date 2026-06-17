using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FocusPanel.Models;
using FocusPanel.ViewModels;

namespace FocusPanel.Views;

public partial class FileOrganizerView : UserControl
{
    // Auto-scroll Timer
    private DispatcherTimer _autoScrollTimer;
    private double _scrollSpeed = 0;
    private ScrollViewer _scrollViewer;

    public FileOrganizerView()
    {
        InitializeComponent();
        _autoScrollTimer = new DispatcherTimer();
        _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(20);
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;
    }

    private void AutoScrollTimer_Tick(object sender, EventArgs e)
    {
        if (_scrollViewer != null && _scrollSpeed != 0)
        {
            _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + _scrollSpeed);
        }
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

    private void FileCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement card && card.DataContext is DesktopFile file)
        {
            if (DataContext is FileOrganizerViewModel vm)
            {
                vm.SelectedFile = file;
                var data = new DataObject();
                data.SetData(typeof(DesktopFile), file);
                DragDrop.DoDragDrop(card, data, DragDropEffects.Move);
            }
        }
    }

    private void Partition_DragOver(object sender, DragEventArgs e)
    {
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
    private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child != null && child is T)
                return (T)child;
            else
            {
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
        }
        return null;
    }

    private void Partition_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = (Brush)FindResource("PrimaryHueMidBrush");
            border.Background = (Brush)FindResource("OrganizerCardBrush");
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
         // Stop auto-scroll when leaving the entire control
         _scrollSpeed = 0;
         _autoScrollTimer.Stop();
    }

    private void Partition_Drop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
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
                        vm.ImportFiles(files, partition.Name);
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
        border.BorderBrush = (Brush)FindResource("OrganizerCardBorderBrush");
        border.BorderThickness = new Thickness(1);
        border.Background = (Brush)FindResource("OrganizerCardBrush");
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
        if (sender is Border border && border.Tag is string colStr && int.TryParse(colStr, out int targetColumn) &&
            DataContext is FileOrganizerViewModel vm &&
            e.Data.GetData(typeof(PartitionViewModel)) is PartitionViewModel sourcePartition)
        {
            e.Handled = true;
            // Move to end of target column
            vm.MovePartitionToColumn(sourcePartition, targetColumn);
        }
    }
}
