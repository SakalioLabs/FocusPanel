using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WorkspaceNavigationStateTests
{
    [Theory]
    [InlineData("Dashboard", 0)]
    [InlineData("Files", 1)]
    [InlineData("Tasks", 2)]
    [InlineData("Pomodoro", 3)]
    [InlineData("AI", 4)]
    public void Compose_SelectsExactlyOneWorkspace(
        string destination,
        int expectedIndex)
    {
        WorkspaceNavigationState state =
            WorkspaceNavigationStateComposer.Compose(
                destination,
                false);
        bool[] values =
        {
            state.Dashboard,
            state.Files,
            state.Tasks,
            state.Pomodoro,
            state.Ai,
            state.Settings
        };

        Assert.Equal(1, state.ActiveCount);
        Assert.True(values[expectedIndex]);
    }

    [Fact]
    public void Compose_SettingsSuppressesUnderlyingWorkspace()
    {
        WorkspaceNavigationState state =
            WorkspaceNavigationStateComposer.Compose(
                "Files",
                true);

        Assert.True(state.Settings);
        Assert.False(state.Files);
        Assert.Equal(1, state.ActiveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("files")]
    public void Compose_UnknownDestinationDoesNotGuess(
        string? destination)
    {
        WorkspaceNavigationState state =
            WorkspaceNavigationStateComposer.Compose(
                destination,
                false);

        Assert.Equal(0, state.ActiveCount);
    }
}
