using System;
using System.Collections.Generic;

namespace FocusPanel.Models;

public sealed record DashboardTaskSummary(
    int Id,
    string Title,
    string ProjectName,
    string Status);

public sealed record DashboardOkrSummary(
    int Id,
    string Name,
    double Progress);

public sealed record DashboardSnapshot(
    int OpenTaskCount,
    int FocusSessionCountToday,
    int FocusMinutesToday,
    int ActiveOkrCount,
    int CollectedItemCount,
    IReadOnlyList<DashboardTaskSummary> PriorityTasks,
    IReadOnlyList<DashboardOkrSummary> ActiveObjectives,
    DateTime LoadedAt);
