using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OkrWorkspaceApplyPolicyTests
{
    [Theory]
    [InlineData(true, 4, 4, true)]
    [InlineData(true, 3, 4, false)]
    [InlineData(false, 4, 4, false)]
    public void CanApply_RequiresValidMatchingRevision(
        bool isValid,
        long capturedRevision,
        long currentRevision,
        bool expected)
    {
        var snapshot =
            new OkrWorkspaceSnapshot(
                isValid,
                Array.Empty<OkrObjective>(),
                false,
                30,
                null,
                null);

        bool actual =
            OkrWorkspaceApplyPolicy.CanApply(
                snapshot,
                capturedRevision,
                currentRevision);

        Assert.Equal(expected, actual);
    }
}
