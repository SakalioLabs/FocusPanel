using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FocusPanel.Services;

internal interface IDesktopToggleNative
{
    bool ToggleDesktop();
}

internal sealed class ShellDesktopToggleNative :
    IDesktopToggleNative
{
    public bool ToggleDesktop()
    {
        object? shell = null;
        try
        {
            Type? shellType =
                Type.GetTypeFromProgID(
                    "Shell.Application");
            if (shellType == null)
                return false;

            shell = Activator.CreateInstance(shellType);
            if (shell == null)
                return false;

            shellType.InvokeMember(
                "ToggleDesktop",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: null);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (shell != null
                && Marshal.IsComObject(shell))
            {
                try
                {
                    Marshal.FinalReleaseComObject(shell);
                }
                catch
                {
                    // The Shell host can disappear during Explorer restart.
                }
            }
        }
    }
}
