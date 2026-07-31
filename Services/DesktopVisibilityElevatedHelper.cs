using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace FocusPanel.Services;

public static class DesktopVisibilityElevatedHelper
{
    public const string Command = "--desktop-attributes-helper";
    public const string SessionCommand =
        "--desktop-attributes-session";
    private const byte CompleteSession = 0;
    private const byte SetAttributesRequest = 1;

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

    public static int RunSession(string[] args)
    {
        if (args.Length != 2
            || !string.Equals(
                args[0],
                SessionCommand,
                StringComparison.OrdinalIgnoreCase)
            || !args[1].StartsWith(
                "FocusPanel.DesktopAttributes.",
                StringComparison.Ordinal))
        {
            return 2;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                args[1],
                PipeDirection.InOut,
                PipeOptions.None);
            pipe.Connect(120_000);
            using var reader = new BinaryReader(
                pipe,
                System.Text.Encoding.UTF8,
                true);
            using var writer = new BinaryWriter(
                pipe,
                System.Text.Encoding.UTF8,
                true);
            while (true)
            {
                byte request = reader.ReadByte();
                if (request == CompleteSession)
                    return 0;
                if (request != SetAttributesRequest)
                    return 3;

                string path = reader.ReadString();
                long attributes = reader.ReadInt64();
                int result = ApplyValidatedAttributes(
                    path,
                    (FileAttributes)attributes);
                writer.Write(result);
                writer.Flush();
            }
        }
        catch (EndOfStreamException)
        {
            // The normal process closed or crashed. The elevated helper
            // must end instead of remaining as a detached process.
            return 0;
        }
        catch
        {
            return 5;
        }
    }

    internal static IDesktopVisibilityElevatedBatch
        StartBatch()
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "无法确定 FocusPanel 可执行文件路径。");
        string pipeName =
            "FocusPanel.DesktopAttributes."
            + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
            | PipeOptions.CurrentUserOnly);
        Process? helper = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add(SessionCommand);
            startInfo.ArgumentList.Add(pipeName);
            helper = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "无法启动公共桌面批量收纳助手。");

            using var timeout =
                new CancellationTokenSource(
                    TimeSpan.FromMinutes(2));
            pipe.WaitForConnectionAsync(
                    timeout.Token)
                .GetAwaiter()
                .GetResult();
            return new ElevatedBatch(
                pipe,
                helper);
        }
        catch (Win32Exception ex)
            when (ex.NativeErrorCode == 1223)
        {
            pipe.Dispose();
            helper?.Dispose();
            throw new OperationCanceledException(
                "已取消管理员授权。",
                ex);
        }
        catch
        {
            pipe.Dispose();
            helper?.Dispose();
            throw;
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

    private static int ApplyValidatedAttributes(
        string path,
        FileAttributes attributes)
    {
        string commonDesktop = Environment.GetFolderPath(
            Environment.SpecialFolder
                .CommonDesktopDirectory);
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return 3;
        }

        if (DesktopDropPolicy.Classify(
                fullPath,
                "",
                commonDesktop)
            != DesktopDropLocation.CommonDesktop
            || (!File.Exists(fullPath)
                && !Directory.Exists(fullPath)))
        {
            return 4;
        }

        try
        {
            var visibility =
                new WindowsDesktopItemVisibilityService();
            visibility.SetAttributes(
                fullPath,
                attributes);
            visibility.NotifyAttributesChanged(
                fullPath);
            return 0;
        }
        catch
        {
            return 5;
        }
    }

    private sealed class ElevatedBatch
        : IDesktopVisibilityElevatedBatch
    {
        private readonly object _gate = new();
        private NamedPipeServerStream? _pipe;
        private Process? _helper;

        internal ElevatedBatch(
            NamedPipeServerStream pipe,
            Process helper)
        {
            _pipe = pipe;
            _helper = helper;
        }

        public void SetAttributes(
            string path,
            FileAttributes attributes)
        {
            lock (_gate)
            {
                NamedPipeServerStream pipe = _pipe
                    ?? throw new ObjectDisposedException(
                        nameof(ElevatedBatch));
                using var writer = new BinaryWriter(
                    pipe,
                    System.Text.Encoding.UTF8,
                    true);
                using var reader = new BinaryReader(
                    pipe,
                    System.Text.Encoding.UTF8,
                    true);
                writer.Write(SetAttributesRequest);
                writer.Write(Path.GetFullPath(path));
                writer.Write((long)attributes);
                writer.Flush();
                int result = reader.ReadInt32();
                if (result != 0)
                {
                    throw new InvalidOperationException(
                        $"公共桌面收纳助手失败（代码 {result}）。");
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                NamedPipeServerStream? pipe =
                    Interlocked.Exchange(
                        ref _pipe,
                        null);
                Process? helper =
                    Interlocked.Exchange(
                        ref _helper,
                        null);
                if (pipe == null)
                {
                    helper?.Dispose();
                    return;
                }

                try
                {
                    pipe.WriteByte(CompleteSession);
                    pipe.Flush();
                }
                catch
                {
                }
                finally
                {
                    pipe.Dispose();
                }

                try
                {
                    helper?.WaitForExit(5_000);
                }
                catch
                {
                }
                finally
                {
                    helper?.Dispose();
                }
            }
        }
    }
}
