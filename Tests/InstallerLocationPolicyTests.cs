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
    public void DefaultLocation_IgnoresSmallRemovableAndOfflineDrives()
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
            @"C:\Users\Test\AppData\Local\FocusPanel",
            selected);
    }
}
