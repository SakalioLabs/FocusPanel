using FocusPanel.Models;

namespace FocusPanel.Services;

internal enum TaskbarPrimaryAction
{
    None,
    ActivateOrMinimize,
    Launch
}

internal static class TaskbarPrimaryActionPolicy
{
    internal static TaskbarPrimaryAction Get(
        TaskbarAppItem? item)
    {
        if (item == null)
            return TaskbarPrimaryAction.None;
        if (item.IsRunning
            && item.WindowCount > 0)
        {
            return TaskbarPrimaryAction
                .ActivateOrMinimize;
        }
        return item.CreateLaunchItem() != null
            ? TaskbarPrimaryAction.Launch
            : TaskbarPrimaryAction.None;
    }
}
