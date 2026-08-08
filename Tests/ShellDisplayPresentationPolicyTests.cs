using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellDisplayPresentationPolicyTests
{
    [Fact]
    public void Create_UsesOneStablePhysicalOrderForCompactAndDetailedNames()
    {
        ShellDisplaySnapshot[] displays =
        {
            new(
                new Rectangle(0, -200, 2560, 1440),
                true,
                @"\\.\DISPLAY1",
                new Rectangle(0, -200, 2560, 1400)),
            new(
                new Rectangle(-1920, 0, 1920, 1080),
                false,
                @"\\.\DISPLAY2")
        };

        var presentations =
            ShellDisplayPresentationPolicy
                .Create(displays);

        Assert.Collection(
            presentations,
            left =>
            {
                Assert.Equal(
                    @"\\.\DISPLAY2",
                    left.DeviceName);
                Assert.Equal(
                    "显示器 1",
                    left.CompactName);
                Assert.Equal(
                    "显示器 1 · 1920×1080 · (-1920,0)",
                    left.DisplayName);
                Assert.Equal(0, left.OrderIndex);
                Assert.Equal(
                    displays[1].Bounds,
                    left.WorkArea);
            },
            primary =>
            {
                Assert.Equal(
                    "显示器 2 · 主屏",
                    primary.CompactName);
                Assert.Equal(
                    displays[0].WorkingArea,
                    primary.WorkArea);
                Assert.True(primary.IsPrimary);
                Assert.Equal(1, primary.OrderIndex);
            });
    }

    [Fact]
    public void Create_FiltersInvalidDisplaysAndFallsBackToBounds()
    {
        var presentations =
            ShellDisplayPresentationPolicy
                .Create(
                    new[]
                    {
                        new ShellDisplaySnapshot(
                            Rectangle.Empty,
                            true,
                            "INVALID"),
                        new ShellDisplaySnapshot(
                            new Rectangle(10, 20, 800, 600),
                            false,
                            "DISPLAY")
                    });

        ShellDisplayPresentation result =
            Assert.Single(presentations);
        Assert.Equal(
            result.Bounds,
            result.WorkArea);
    }
}
