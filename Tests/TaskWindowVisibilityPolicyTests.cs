using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskWindowVisibilityPolicyTests
{
    [Theory]
    [InlineData(true, false, false, false, false, false, true)]
    [InlineData(true, false, false, true, true, false, true)]
    [InlineData(true, false, false, true, false, false, false)]
    [InlineData(false, false, false, false, false, false, false)]
    [InlineData(true, true, false, false, false, false, false)]
    [InlineData(true, false, true, false, false, false, false)]
    [InlineData(true, false, false, false, false, true, false)]
    public void ShouldInclude_MatchesTaskbarWindowSemantics(
        bool isVisible,
        bool isToolWindow,
        bool isNoActivate,
        bool hasOwner,
        bool isAppWindow,
        bool isCloaked,
        bool expected)
    {
        Assert.Equal(
            expected,
            TaskWindowVisibilityPolicy.ShouldInclude(
                isVisible,
                isToolWindow,
                isNoActivate,
                hasOwner,
                isAppWindow,
                isCloaked));
    }
}
