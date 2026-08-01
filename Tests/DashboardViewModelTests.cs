using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task Refresh_AppliesLocalSnapshot()
    {
        var snapshot = new DashboardSnapshot(
            7,
            2,
            50,
            12,
            new[]
            {
                new DashboardTaskSummary(
                    1,
                    "完成概览",
                    "FocusPanel",
                    "进行中")
            },
            new DateTime(2026, 7, 28, 15, 20, 0));
        using var viewModel =
            new DashboardViewModel(
                new FakeDashboardDataService(snapshot));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(7, viewModel.OpenTaskCount);
        Assert.Equal(2, viewModel.FocusSessionCountToday);
        Assert.Equal(50, viewModel.FocusMinutesToday);
        Assert.Equal(12, viewModel.CollectedItemCount);
        Assert.True(viewModel.HasTasks);
        Assert.Contains("更新于 15:20", viewModel.StatusText);
        Assert.Contains("星期二", viewModel.DateText);
    }

    [Fact]
    public async Task Refresh_EmptySnapshotShowsEmptyStates()
    {
        var snapshot = new DashboardSnapshot(
            0,
            0,
            0,
            0,
            Array.Empty<DashboardTaskSummary>(),
            DateTime.Now);
        using var viewModel =
            new DashboardViewModel(
                new FakeDashboardDataService(snapshot));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasTasks);
        Assert.Empty(viewModel.PriorityTasks);
    }

    [Fact]
    public void Navigate_RaisesRequestedDestination()
    {
        using var viewModel =
            new DashboardViewModel(
                new FakeDashboardDataService(
                    EmptySnapshot()));
        string? destination = null;
        viewModel.NavigationRequested +=
            value => destination = value;

        viewModel.NavigateCommand.Execute("Pomodoro");

        Assert.Equal("Pomodoro", destination);
    }

    private static DashboardSnapshot EmptySnapshot() =>
        new(
            0,
            0,
            0,
            0,
            Array.Empty<DashboardTaskSummary>(),
            DateTime.Now);

    private sealed class FakeDashboardDataService :
        IDashboardDataService
    {
        private readonly DashboardSnapshot _snapshot;

        internal FakeDashboardDataService(
            DashboardSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<DashboardSnapshot> LoadAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(_snapshot);
    }
}
