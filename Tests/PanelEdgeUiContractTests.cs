using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelEdgeUiContractTests
{
    [Fact]
    public void Shell_MirrorsEveryEdgeOwnedSurface()
    {
        string root = FindRepositoryRoot();
        string xaml = Read(
            root,
            "Views",
            "MainWindow.xaml");
        string shell = Read(
            root,
            "Views",
            "MainWindow.xaml.cs");
        string indicator = Read(
            root,
            "Views",
            "EdgeIndicatorWindow.xaml.cs");
        string preview = Read(
            root,
            "Views",
            "TaskbarWindowPreviewWindow.xaml.cs");
        string toast = Read(
            root,
            "Services",
            "FocusToastManager.cs");

        Assert.Contains(
            "SelectedValue=\"{Binding PanelEdge",
            xaml);
        Assert.Contains(
            "Click=\"PanelEdgeMenuItem_Click\"",
            xaml);
        Assert.Contains(
            "Grid.SetColumn(\n            CompactDock,",
            Normalize(shell));
        Assert.Contains(
            "Grid.SetColumn(\n            WorkspaceHost,",
            Normalize(shell));
        Assert.Contains(
            "_hotZoneMonitor?.SetPanelEdge(",
            shell);
        Assert.Contains(
            "_edgeIndicator.EdgeValue =",
            shell);
        Assert.Contains(
            "ShellPanelEdgePolicy\n"
            + "                .IsLeft(panelEdge)",
            Normalize(preview));
        Assert.Contains(
            "anchorNearEdgePhysical\n"
            + "                    + PreviewGapPhysical",
            Normalize(preview));
        Assert.Contains(
            "PanelEdgeValue",
            toast);
        Assert.Contains(
            "ShellPanelEdgePolicy.IsLeft(",
            toast);
        Assert.Contains(
            "EdgeValue",
            indicator);
    }

    private static string Read(
        string root,
        params string[] parts)
    {
        string path = root;
        foreach (string part in parts)
            path = Path.Combine(path, part);
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot() =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                ".."));

    private static string Normalize(
        string value) =>
        value.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
}
