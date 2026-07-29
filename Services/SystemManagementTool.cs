namespace FocusPanel.Services;

public enum SystemManagementTool
{
    InstalledApps,
    PowerOptions,
    EventViewer,
    SystemAbout,
    DeviceManager,
    NetworkConnections,
    DiskManagement,
    ComputerManagement,
    Terminal,
    TerminalAdministrator,
    TaskManager,
    Settings,
    FileExplorer,
    DateAndTimeSettings,
    NotificationSettings
}

internal readonly record struct SystemLaunchRequest(
    string FileName,
    string? Arguments = null,
    string? Verb = null,
    string? FallbackFileName = null);

internal static class SystemManagementToolCatalog
{
    internal static SystemLaunchRequest Get(SystemManagementTool tool) => tool switch
    {
        SystemManagementTool.InstalledApps => new("ms-settings:appsfeatures"),
        SystemManagementTool.PowerOptions => new("powercfg.cpl"),
        SystemManagementTool.EventViewer => new("eventvwr.msc"),
        SystemManagementTool.SystemAbout => new("ms-settings:about"),
        SystemManagementTool.DeviceManager => new("devmgmt.msc"),
        SystemManagementTool.NetworkConnections => new("ncpa.cpl"),
        SystemManagementTool.DiskManagement => new("diskmgmt.msc"),
        SystemManagementTool.ComputerManagement => new("compmgmt.msc"),
        SystemManagementTool.Terminal => new(
            "wt.exe",
            FallbackFileName: "powershell.exe"),
        SystemManagementTool.TerminalAdministrator => new(
            "wt.exe",
            Verb: "runas",
            FallbackFileName: "powershell.exe"),
        SystemManagementTool.TaskManager => new("taskmgr.exe"),
        SystemManagementTool.Settings => new("ms-settings:"),
        SystemManagementTool.FileExplorer => new("explorer.exe"),
        SystemManagementTool.DateAndTimeSettings =>
            new("ms-settings:dateandtime"),
        SystemManagementTool.NotificationSettings =>
            new("ms-settings:notifications"),
        _ => throw new System.ArgumentOutOfRangeException(nameof(tool), tool, null)
    };
}
