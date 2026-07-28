using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal interface IAppCatalogSource
{
    IEnumerable<AppLaunchItem> EnumerateStartMenuApps();
    IEnumerable<AppLaunchItem> EnumerateShellApps();
}

internal sealed class WindowsAppCatalogSource : IAppCatalogSource
{
    public IEnumerable<AppLaunchItem> EnumerateStartMenuApps()
    {
        string[] roots =
        {
            Environment.GetFolderPath(
                Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (string root in roots)
        {
            foreach (string shortcut in
                     AppCatalogService.SafeEnumerateShortcuts(root))
            {
                string displayName =
                    System.IO.Path.GetFileNameWithoutExtension(
                        shortcut);
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                yield return new AppLaunchItem
                {
                    DisplayName = displayName,
                    LaunchKind = AppLaunchKind.Shortcut,
                    LaunchTarget = shortcut,
                    IconKey = shortcut
                };
            }
        }
    }

    public IEnumerable<AppLaunchItem> EnumerateShellApps()
    {
        object? shellObject = null;
        try
        {
            Type? shellType =
                Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
                yield break;

            shellObject = Activator.CreateInstance(shellType);
            if (shellObject == null)
                yield break;

            dynamic shell = shellObject;
            dynamic folder =
                shell.NameSpace("shell:AppsFolder");
            if (folder == null)
                yield break;

            foreach (dynamic item in folder.Items())
            {
                string name =
                    item.Name as string ?? string.Empty;
                string path =
                    item.Path as string ?? string.Empty;
                if (name.Length == 0 || path.Length == 0)
                    continue;

                yield return new AppLaunchItem
                {
                    DisplayName = name,
                    LaunchKind = AppLaunchKind.ShellApp,
                    LaunchTarget = path,
                    IconKey = path
                };
            }
        }
        finally
        {
            if (shellObject != null
                && Marshal.IsComObject(shellObject))
            {
                Marshal.FinalReleaseComObject(shellObject);
            }
        }
    }
}
