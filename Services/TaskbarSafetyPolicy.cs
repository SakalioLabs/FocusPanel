namespace FocusPanel.Services;

internal static class TaskbarSafetyPolicy
{
    internal static bool TryValidatePrerequisites(
        bool taskbarFound,
        out string? error)
    {
        if (!taskbarFound)
        {
            error = "没有找到 Windows 原生任务栏。";
            return false;
        }

        error = null;
        return true;
    }
}
