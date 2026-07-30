using System.Collections.Generic;

namespace FocusPanel.Services;

internal readonly record struct SystemManagementSearchEntry(
    SystemManagementTool Tool,
    string DisplayName,
    string Glyph,
    string Aliases);

internal static class SystemManagementSearchCatalog
{
    internal static IReadOnlyList<
        SystemManagementSearchEntry> All { get; } =
        new[]
        {
            new SystemManagementSearchEntry(
                SystemManagementTool.TaskManager,
                "任务管理器",
                "\uE9D9",
                "任务 进程 性能 启动应用 task manager taskmgr"),
            new SystemManagementSearchEntry(
                SystemManagementTool.Settings,
                "设置",
                "\uE713",
                "系统设置 Windows 设置 settings ms-settings"),
            new SystemManagementSearchEntry(
                SystemManagementTool.FileExplorer,
                "文件资源管理器",
                "\uE8B7",
                "资源管理器 文件夹 此电脑 explorer file manager"),
            new SystemManagementSearchEntry(
                SystemManagementTool.Terminal,
                "终端",
                "\uE756",
                "命令行 PowerShell 命令提示符 terminal shell wt"),
            new SystemManagementSearchEntry(
                SystemManagementTool.TerminalAdministrator,
                "终端（管理员）",
                "\uE7EF",
                "管理员终端 管理员命令行 admin terminal PowerShell runas"),
            new SystemManagementSearchEntry(
                SystemManagementTool.InstalledApps,
                "安装的应用",
                "\uE71D",
                "应用和功能 卸载程序 installed apps apps features"),
            new SystemManagementSearchEntry(
                SystemManagementTool.PowerOptions,
                "电源选项",
                "\uE7E8",
                "电池 睡眠 电源计划 power options battery sleep"),
            new SystemManagementSearchEntry(
                SystemManagementTool.EventViewer,
                "事件查看器",
                "\uE9D5",
                "系统日志 事件日志 event viewer eventvwr"),
            new SystemManagementSearchEntry(
                SystemManagementTool.SystemAbout,
                "系统信息",
                "\uE946",
                "关于电脑 设备规格 Windows 版本 system about info"),
            new SystemManagementSearchEntry(
                SystemManagementTool.DeviceManager,
                "设备管理器",
                "\uE772",
                "硬件 驱动 设备 device manager devmgmt"),
            new SystemManagementSearchEntry(
                SystemManagementTool.NetworkConnections,
                "网络连接",
                "\uE968",
                "网卡 适配器 WiFi 以太网 network connections ncpa"),
            new SystemManagementSearchEntry(
                SystemManagementTool.DiskManagement,
                "磁盘管理",
                "\uEDA2",
                "硬盘 分区 卷 disk management diskmgmt"),
            new SystemManagementSearchEntry(
                SystemManagementTool.ComputerManagement,
                "计算机管理",
                "\uE770",
                "系统工具 存储 服务 computer management compmgmt"),
            new SystemManagementSearchEntry(
                SystemManagementTool.DateAndTimeSettings,
                "日期和时间设置",
                "\uE787",
                "时区 时间同步 date time timezone"),
            new SystemManagementSearchEntry(
                SystemManagementTool.NotificationSettings,
                "通知设置",
                "\uE7F4",
                "通知中心 勿扰 notification settings do not disturb")
        };
}
