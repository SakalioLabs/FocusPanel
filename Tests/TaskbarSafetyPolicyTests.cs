using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarSafetyPolicyTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void MissingPrerequisite_PreventsReplacement(
        bool taskbarFound,
        bool primaryFound,
        bool workAreaRead)
    {
        bool allowed = TaskbarSafetyPolicy.TryValidatePrerequisites(
            taskbarFound,
            primaryFound,
            workAreaRead,
            out string? error);

        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void CompletePrerequisites_AllowReplacement()
    {
        bool allowed = TaskbarSafetyPolicy.TryValidatePrerequisites(
            true,
            true,
            true,
            out string? error);

        Assert.True(allowed);
        Assert.Null(error);
    }
}
