using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarSafetyPolicyTests
{
    [Fact]
    public void MissingTaskbar_PreventsReplacement()
    {
        bool allowed = TaskbarSafetyPolicy.TryValidatePrerequisites(
            false,
            out string? error);

        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void CompletePrerequisites_AllowReplacement()
    {
        bool allowed = TaskbarSafetyPolicy.TryValidatePrerequisites(
            true,
            out string? error);

        Assert.True(allowed);
        Assert.Null(error);
    }
}
