using System;
using FocusPanel.Helpers;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopDropTargetPolicyTests
{
    [Fact]
    public void CursorTargetCanBeQueriedWithoutMarshallingFailure()
    {
        Exception? exception = Record.Exception(() => DesktopHelper.IsCursorOverDesktop());
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("SHELLDLL_DefView")]
    [InlineData("SysListView32")]
    public void DesktopShellClassesAreAccepted(string className)
        => Assert.True(DesktopDropTargetPolicy.IsDesktopWindowClass(className));

    [Theory]
    [InlineData("")]
    [InlineData("CabinetWClass")]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("Shell_TrayWnd")]
    public void OtherWindowClassesAreRejected(string className)
        => Assert.False(DesktopDropTargetPolicy.IsDesktopWindowClass(className));
}
