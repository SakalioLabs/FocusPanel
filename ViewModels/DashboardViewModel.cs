using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Models;
using FocusPanel.Services;

namespace FocusPanel.ViewModels;

public partial class DashboardViewModel :
    ObservableObject,
    IDisposable
{
    private readonly IDashboardDataService _dataService;
    private CancellationTokenSource? _refreshCancellation;
    private bool _disposed;

    [ObservableProperty]
    private string greeting = "今天，从一件重要的事开始";

    [ObservableProperty]
    private string dateText = string.Empty;

    [ObservableProperty]
    private string statusText = "正在准备今日概览…";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private int openTaskCount;

    [ObservableProperty]
    private int focusSessionCountToday;

    [ObservableProperty]
    private int focusMinutesToday;

    [ObservableProperty]
    private int activeOkrCount;

    [ObservableProperty]
    private int collectedItemCount;

    [ObservableProperty]
    private bool hasTasks;

    [ObservableProperty]
    private bool hasObjectives;

    public DashboardViewModel()
        : this(new DashboardDataService())
    {
    }

    internal DashboardViewModel(
        IDashboardDataService dataService)
    {
        _dataService = dataService;
        UpdateClockText(DateTime.Now);
    }

    public ObservableCollection<DashboardTaskSummary>
        PriorityTasks { get; } = new();

    public ObservableCollection<DashboardOkrSummary>
        ActiveObjectives { get; } = new();

    public event Action<string>? NavigationRequested;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_disposed)
            return;

        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken =
            _refreshCancellation.Token;
        IsLoading = true;
        StatusText = "正在刷新本地概览…";

        try
        {
            DashboardSnapshot snapshot =
                await _dataService.LoadAsync(cancellationToken);
            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText = $"暂时无法读取概览：{ex.Message}";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                IsLoading = false;
        }
    }

    [RelayCommand]
    private void Navigate(string? destination)
    {
        if (!string.IsNullOrWhiteSpace(destination))
            NavigationRequested?.Invoke(destination);
    }

    internal void ApplySnapshot(
        DashboardSnapshot snapshot)
    {
        OpenTaskCount = snapshot.OpenTaskCount;
        FocusSessionCountToday =
            snapshot.FocusSessionCountToday;
        FocusMinutesToday = snapshot.FocusMinutesToday;
        ActiveOkrCount = snapshot.ActiveOkrCount;
        CollectedItemCount = snapshot.CollectedItemCount;
        ReplaceCollection(
            PriorityTasks,
            snapshot.PriorityTasks);
        ReplaceCollection(
            ActiveObjectives,
            snapshot.ActiveObjectives.Select(
                item => item with
                {
                    Progress = Math.Clamp(
                        item.Progress,
                        0,
                        100)
                }));
        HasTasks = PriorityTasks.Count > 0;
        HasObjectives = ActiveObjectives.Count > 0;
        UpdateClockText(snapshot.LoadedAt);
        StatusText =
            $"更新于 {snapshot.LoadedAt:HH:mm} · 数据仅来自本机 FocusPanel";
    }

    private void UpdateClockText(DateTime now)
    {
        Greeting = now.Hour switch
        {
            < 6 => "夜深了，先收好今天的尾巴",
            < 11 => "早上好，先完成最重要的一件事",
            < 14 => "中午好，给下午留出清晰的下一步",
            < 18 => "下午好，把注意力放回当前目标",
            _ => "晚上好，收束今天并准备明天"
        };
        DateText = now.ToString(
            "M 月 d 日 dddd",
            CultureInfo.GetCultureInfo("zh-CN"));
    }

    private static void ReplaceCollection<T>(
        ObservableCollection<T> destination,
        System.Collections.Generic.IEnumerable<T> source)
    {
        destination.Clear();
        foreach (T item in source)
            destination.Add(item);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        NavigationRequested = null;
    }
}
