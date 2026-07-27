using System.Collections.Generic;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PinnedAppOrderingTests
{
    [Fact]
    public void Move_ReordersAndClampsIndex()
    {
        var items = new List<string> { "A", "B", "C" };

        PinnedAppOrdering.Move(items, "A", 99);

        Assert.Equal(new[] { "B", "C", "A" }, items);
    }

    [Fact]
    public void Move_IgnoresMissingItem()
    {
        var items = new List<string> { "A", "B" };

        PinnedAppOrdering.Move(items, "C", 0);

        Assert.Equal(new[] { "A", "B" }, items);
    }
}
