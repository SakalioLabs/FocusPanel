using System;
using System.Collections.Generic;
using System.IO;

namespace FocusPanel.Services
{
    internal sealed class InstallerDriveCandidate
    {
        internal InstallerDriveCandidate(
            string rootDirectory,
            DriveType driveType,
            bool isReady,
            long availableFreeSpace)
        {
            RootDirectory = rootDirectory;
            DriveType = driveType;
            IsReady = isReady;
            AvailableFreeSpace =
                availableFreeSpace;
        }

        internal string RootDirectory { get; private set; }
        internal DriveType DriveType { get; private set; }
        internal bool IsReady { get; private set; }
        internal long AvailableFreeSpace { get; private set; }
    }

    internal static class InstallerLocationPolicy
    {
        internal const long MinimumRecommendedFreeSpace =
            512L * 1024 * 1024;

        internal static string SelectDefaultDirectory(
            IEnumerable<InstallerDriveCandidate> drives,
            string systemRoot,
            string localAppData)
        {
            InstallerDriveCandidate best =
                new InstallerDriveCandidate(
                    string.Empty,
                    DriveType.Unknown,
                    false,
                    -1);
            bool found = false;
            foreach (InstallerDriveCandidate drive
                     in drives)
            {
                if (!drive.IsReady
                    || drive.DriveType
                        != DriveType.Fixed
                    || drive.AvailableFreeSpace
                        < MinimumRecommendedFreeSpace
                    || SameRoot(
                        drive.RootDirectory,
                        systemRoot))
                {
                    continue;
                }

                if (!found
                    || drive.AvailableFreeSpace
                        > best.AvailableFreeSpace)
                {
                    best = drive;
                    found = true;
                }
            }

            if (!found)
            {
                return Path.Combine(
                    localAppData,
                    "FocusPanel");
            }

            return Path.Combine(
                best.RootDirectory,
                "Applications",
                "FocusPanel");
        }

        private static bool SameRoot(
            string first,
            string second)
        {
            string firstRoot =
                NormalizeRoot(first);
            string secondRoot =
                NormalizeRoot(second);
            return firstRoot.Length > 0
                && string.Equals(
                    firstRoot,
                    secondRoot,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRoot(
            string path)
        {
            try
            {
                string root =
                    Path.GetPathRoot(path)
                    ?? string.Empty;
                return root.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool HasInstalledExecutable(
            string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            try
            {
                string root =
                    Path.GetFullPath(directory);
                return File.Exists(
                        Path.Combine(
                            root,
                            "current",
                            "FocusPanel.exe"))
                    || File.Exists(
                        Path.Combine(
                            root,
                            "FocusPanel.exe"));
            }
            catch
            {
                return false;
            }
        }
    }
}
