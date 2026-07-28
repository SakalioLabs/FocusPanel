using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class OkrProgressCalculator
{
    internal static double CalculateKeyResultProgress(
        double start,
        double current,
        double target)
    {
        double range = target - start;
        if (Math.Abs(range) < double.Epsilon)
            return current.Equals(target) ? 100 : 0;

        double progress =
            (current - start) / range * 100;
        return Clamp(progress);
    }

    internal static double CalculateObjectiveProgress(
        IEnumerable<OkrKeyResult> keyResults)
    {
        List<OkrKeyResult> active =
            keyResults
                .Where(result => !result.IsDeleted)
                .ToList();
        if (active.Count == 0)
            return 0;

        double positiveWeight =
            active
                .Where(result => result.Weight > 0)
                .Sum(result => result.Weight);
        double progress = positiveWeight > 0
            ? active
                .Where(result => result.Weight > 0)
                .Sum(result =>
                    Clamp(result.Progress)
                    * result.Weight)
                / positiveWeight
            : active.Average(result =>
                Clamp(result.Progress));
        return Clamp(progress);
    }

    private static double Clamp(double value)
        => Math.Max(0, Math.Min(100, value));
}
