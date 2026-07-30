using System.Collections.Generic;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemManagementToolCatalogTests
{
    [Fact]
    public void SearchCatalog_CoversEveryManagementToolExactlyOnce()
    {
        Assert.Equal(
            System.Enum.GetValues<
                SystemManagementTool>().Length,
            SystemManagementSearchCatalog
                .All.Count);
        Assert.Equal(
            SystemManagementSearchCatalog
                .All.Count,
            SystemManagementSearchCatalog
                .All
                .Select(item => item.Tool)
                .Distinct()
                .Count());
        Assert.All(
            SystemManagementSearchCatalog
                .All,
            entry =>
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        entry.DisplayName));
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        entry.Glyph));
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        entry.Aliases));
            });
    }

    public static IEnumerable<object[]> ToolRequests()
    {
        yield return Case(SystemManagementTool.InstalledApps, "ms-settings:appsfeatures");
        yield return Case(SystemManagementTool.PowerOptions, "powercfg.cpl");
        yield return Case(SystemManagementTool.EventViewer, "eventvwr.msc");
        yield return Case(SystemManagementTool.SystemAbout, "ms-settings:about");
        yield return Case(SystemManagementTool.DeviceManager, "devmgmt.msc");
        yield return Case(SystemManagementTool.NetworkConnections, "ncpa.cpl");
        yield return Case(SystemManagementTool.DiskManagement, "diskmgmt.msc");
        yield return Case(SystemManagementTool.ComputerManagement, "compmgmt.msc");
        yield return Case(SystemManagementTool.Terminal, "wt.exe");
        yield return Case(SystemManagementTool.TaskManager, "taskmgr.exe");
        yield return Case(SystemManagementTool.Settings, "ms-settings:");
        yield return Case(SystemManagementTool.FileExplorer, "explorer.exe");
        yield return Case(
            SystemManagementTool.DateAndTimeSettings,
            "ms-settings:dateandtime");
        yield return Case(
            SystemManagementTool.NotificationSettings,
            "ms-settings:notifications");
    }

    [Theory]
    [MemberData(nameof(ToolRequests))]
    public void Tool_MapsToExpectedWindowsEntryPoint(
        SystemManagementTool tool,
        string expectedFile)
    {
        SystemLaunchRequest request = SystemManagementToolCatalog.Get(tool);

        Assert.Equal(expectedFile, request.FileName);
        Assert.Null(request.Verb);
    }

    [Fact]
    public void AdministratorTerminal_UsesRunAsVerb()
    {
        SystemLaunchRequest request = SystemManagementToolCatalog.Get(
            SystemManagementTool.TerminalAdministrator);

        Assert.Equal("wt.exe", request.FileName);
        Assert.Equal("runas", request.Verb);
        Assert.Equal("powershell.exe", request.FallbackFileName);
    }

    private static object[] Case(SystemManagementTool tool, string fileName) =>
        new object[] { tool, fileName };
}
