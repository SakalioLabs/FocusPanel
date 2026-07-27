using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace FocusPanel.Services;

public static class DesktopVisibilityElevatedHelper
{
    public const string Command = "--desktop-attributes-helper";

    public static int Run(string[] args)
    {
        if (args.Length != 3
            || !string.Equals(args[0], Command, StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long rawAttributes))
            return 2;

        string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(args[1]);
        }
        catch
        {
            return 3;
        }

        if (DesktopDropPolicy.Classify(fullPath, "", commonDesktop) != DesktopDropLocation.CommonDesktop
            || (!File.Exists(fullPath) && !Directory.Exists(fullPath)))
            return 4;

        try
        {
            var visibility = new WindowsDesktopItemVisibilityService();
            visibility.SetAttributes(fullPath, (FileAttributes)rawAttributes);
            visibility.NotifyAttributesChanged(fullPath);
            return 0;
        }
        catch
        {
            return 5;
        }
    }

    public static void SetAttributes(string path, FileAttributes attributes)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定 FocusPanel 可执行文件路径。");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add(Command);
        startInfo.ArgumentList.Add(Path.GetFullPath(path));
        startInfo.ArgumentList.Add(((long)attributes).ToString(CultureInfo.InvariantCulture));

        try
        {
            using Process? helper = Process.Start(startInfo);
            if (helper == null)
                throw new InvalidOperationException("无法启动公共桌面收纳助手。");
            helper.WaitForExit();
            if (helper.ExitCode != 0)
                throw new InvalidOperationException($"公共桌面收纳助手失败（代码 {helper.ExitCode}）。");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("已取消管理员授权。", ex);
        }
    }
}
