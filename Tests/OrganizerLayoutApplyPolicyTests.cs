using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OrganizerLayoutApplyPolicyTests
{
    [Theory]
    [InlineData(true, 8, 8, true)]
    [InlineData(true, 7, 8, false)]
    [InlineData(false, 8, 8, false)]
    public void CanApplyOptions_RequiresValidMatchingRevision(
        bool valid,
        long capturedRevision,
        long currentRevision,
        bool expected)
    {
        var snapshot =
            new OrganizerLayoutSnapshot(
                valid,
                new OrganizerLayoutOptions(
                    1,
                    false,
                    true,
                    false),
                Array.Empty<
                    OrganizerPartitionSnapshot>(),
                Array.Empty<
                    OrganizerFilePreferenceSnapshot>());

        bool actual =
            OrganizerLayoutApplyPolicy
                .CanApplyOptions(
                    snapshot,
                    capturedRevision,
                    currentRevision);

        Assert.Equal(expected, actual);
    }
}
