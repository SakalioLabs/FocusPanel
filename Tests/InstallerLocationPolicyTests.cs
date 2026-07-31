using System.Collections.Generic;
using System.IO;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class InstallerLocationPolicyTests
{
    [Fact]
    public void DefaultLocation_PrefersLargestReadyNonSystemDrive()
    {
        var drives =
            new List<InstallerDriveCandidate>
            {
                new(
                    @"C:\",
                    DriveType.Fixed,
                    true,
                    80L * 1024 * 1024 * 1024),
                new(
                    @"D:\",
                    DriveType.Fixed,
                    true,
                    20L * 1024 * 1024 * 1024),
                new(
                    @"E:\",
                    DriveType.Fixed,
                    true,
                    60L * 1024 * 1024 * 1024)
            };

        string selected =
            InstallerLocationPolicy
                .SelectDefaultDirectory(
                    drives,
                    @"C:\",
                    @"C:\Users\Test\AppData\Local");

        Assert.Equal(
            @"E:\Applications\FocusPanel",
            selected);
    }

    [Fact]
    public void DefaultLocation_UsesViableFixedDriveEvenWhenSpaceIsLimited()
    {
        var drives =
            new List<InstallerDriveCandidate>
            {
                new(
                    @"D:\",
                    DriveType.Removable,
                    true,
                    100L * 1024 * 1024 * 1024),
                new(
                    @"E:\",
                    DriveType.Fixed,
                    false,
                    100L * 1024 * 1024 * 1024),
                new(
                    @"F:\",
                    DriveType.Fixed,
                    true,
                    1024L * 1024 * 1024)
            };

        string selected =
            InstallerLocationPolicy
                .SelectDefaultDirectory(
                    drives,
                    @"C:\",
                    @"C:\Users\Test\AppData\Local");

        Assert.Equal(
            @"F:\Applications\FocusPanel",
            selected);
    }

    [Fact]
    public void InstallVerification_RequiresExecutableUnderSelectedRoot()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "FocusPanelInstallerPolicy",
                System.Guid.NewGuid().ToString("N"));
        string current =
            Path.Combine(
                root,
                "current");
        try
        {
            Directory.CreateDirectory(current);
            Assert.False(
                InstallerLocationPolicy
                    .HasInstalledExecutable(root));

            File.WriteAllText(
                Path.Combine(
                    current,
                    "FocusPanel.exe"),
                string.Empty);

            Assert.True(
                InstallerLocationPolicy
                    .HasInstalledExecutable(root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void InitialLocation_MigratesExistingSystemDriveInstallToRecommendation()
    {
        Assert.Equal(
            @"D:\Applications\FocusPanel",
            InstallerLocationPolicy
                .SelectInitialDirectory(
                    @"C:\Users\Test\AppData\Local\FocusPanel",
                    @"D:\Applications\FocusPanel",
                    @"C:\"));
    }

    [Fact]
    public void InitialLocation_PreservesExistingNonSystemDriveInstall()
    {
        Assert.Equal(
            @"D:\Applications\FocusPanel",
            InstallerLocationPolicy
                .SelectInitialDirectory(
                    @"D:\Applications\FocusPanel",
                    @"E:\Applications\FocusPanel",
                    @"C:\"));
    }

    [Fact]
    public void InitialLocation_UsesRecommendationWithoutLiveInstall()
    {
        Assert.Equal(
            @"E:\Applications\FocusPanel",
            InstallerLocationPolicy
                .SelectInitialDirectory(
                    string.Empty,
                    @"E:\Applications\FocusPanel",
                    @"C:\"));
    }
}
