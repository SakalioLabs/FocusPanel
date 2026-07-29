using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WorkspaceLoadApplyPolicyTests
{
    [Fact]
    public void MatchingRevision_AppliesPreparedWorkspace()
    {
        Assert.True(
            WorkspaceLoadApplyPolicy.CanApply(
                4,
                4,
                false));
    }

    [Fact]
    public void NewerNavigation_DropsLateWorkspaceResult()
    {
        Assert.False(
            WorkspaceLoadApplyPolicy.CanApply(
                4,
                5,
                false));
    }

    [Fact]
    public void DisposedShell_DropsPreparedWorkspace()
    {
        Assert.False(
            WorkspaceLoadApplyPolicy.CanApply(
                4,
                4,
                true));
    }
}
