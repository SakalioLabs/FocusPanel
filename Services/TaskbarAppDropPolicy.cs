using System;

namespace FocusPanel.Services;

public enum TaskbarDropPlacement
{
    Before,
    After
}

internal static class TaskbarAppDropPolicy
{
    internal static TaskbarDropPlacement?
        GetCuePlacement(
            bool targetIsPinned,
            bool isFirstUnpinned,
            double pointerY,
            double itemHeight)
    {
        if (targetIsPinned)
        {
            return GetPlacement(
                pointerY,
                itemHeight);
        }

        return isFirstUnpinned
            ? TaskbarDropPlacement.Before
            : null;
    }

    internal static TaskbarDropPlacement
        GetPlacement(
            double pointerY,
            double itemHeight)
    {
        if (!double.IsFinite(pointerY)
            || !double.IsFinite(itemHeight)
            || itemHeight <= 0)
        {
            return TaskbarDropPlacement.After;
        }

        return pointerY < itemHeight / 2
            ? TaskbarDropPlacement.Before
            : TaskbarDropPlacement.After;
    }

    internal static int GetInsertionIndex(
        bool sourceIsPinned,
        int sourcePinnedIndex,
        bool targetIsPinned,
        int targetPinnedIndex,
        int pinnedCount,
        TaskbarDropPlacement placement)
    {
        int safePinnedCount =
            Math.Max(0, pinnedCount);
        int maximumIndex = sourceIsPinned
            ? Math.Max(
                0,
                safePinnedCount - 1)
            : safePinnedCount;
        if (!targetIsPinned)
            return maximumIndex;

        int insertionBeforeRemoval =
            Math.Clamp(
                targetPinnedIndex,
                0,
                safePinnedCount)
            + (placement
                == TaskbarDropPlacement.After
                    ? 1
                    : 0);
        if (sourceIsPinned
            && sourcePinnedIndex >= 0
            && sourcePinnedIndex
                < insertionBeforeRemoval)
        {
            insertionBeforeRemoval--;
        }

        return Math.Clamp(
            insertionBeforeRemoval,
            0,
            maximumIndex);
    }
}
