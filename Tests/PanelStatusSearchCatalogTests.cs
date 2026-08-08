using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelStatusSearchCatalogTests
{
    [Fact]
    public void Catalog_CoversEveryPanelStatusDetailOnce()
    {
        StatusCenterDetail[] expected =
        {
            StatusCenterDetail.Network,
            StatusCenterDetail.ApplicationAudio,
            StatusCenterDetail.MediaAndBattery,
            StatusCenterDetail.InputMethod,
            StatusCenterDetail.PanelNotifications
        };

        Assert.Equal(
            expected,
            PanelStatusSearchCatalog.All.Select(
                item => item.Detail));
        Assert.Equal(
            expected.Length,
            PanelStatusSearchCatalog.All
                .Select(item => item.Detail)
                .Distinct()
                .Count());
    }

    [Fact]
    public void Catalog_ProvidesVisibleNameGlyphAndAliases()
    {
        Assert.All(
            PanelStatusSearchCatalog.All,
            entry =>
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(entry.DisplayName));
                Assert.False(
                    string.IsNullOrWhiteSpace(entry.Glyph));
                Assert.False(
                    string.IsNullOrWhiteSpace(entry.Aliases));
            });
    }
}
