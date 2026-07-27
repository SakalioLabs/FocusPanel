namespace FocusPanel.Services;

internal static class TaskbarSafetyPolicy
{
    internal static bool TryValidatePrerequisites(
        bool taskbarFound,
        bool primaryScreenFound,
        bool workAreaRead,
        out string? error)
    {
        if (!taskbarFound || !primaryScreenFound)
        {
            error = "没有找到主任务栏或主显示器。";
            return false;
        }

        if (!workAreaRead)
        {
            error = "无法读取当前 Windows 工作区。";
            return false;
        }

        error = null;
        return true;
    }
}
