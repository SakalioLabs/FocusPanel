using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal readonly record struct PanelRunCommand(
    string FileName,
    string Arguments)
{
    internal string StableKey =>
        "run:"
        + FileName
        + "\n"
        + Arguments;

    internal string DisplayName =>
        $"运行 {FileName}";
}

internal static class PanelRunCommandParser
{
    internal const string Prefix = ">";

    internal static bool IsDraft(
        string? query)
    {
        string text = query?.TrimStart()
            ?? string.Empty;
        return text.StartsWith(
            Prefix,
            StringComparison.Ordinal);
    }

    internal static bool TryParse(
        string? query,
        out PanelRunCommand command)
    {
        command = default;
        string text = query?.TrimStart()
            ?? string.Empty;
        if (!text.StartsWith(
                Prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        string commandLine = text[
                Prefix.Length..]
            .TrimStart();
        if (commandLine.Length == 0)
            return false;

        string fileName;
        string arguments;
        if (commandLine[0] == '"')
        {
            int closingQuote =
                commandLine.IndexOf('"', 1);
            if (closingQuote <= 1)
                return false;

            fileName = commandLine[
                1..closingQuote];
            arguments = commandLine[
                    (closingQuote + 1)..]
                .TrimStart();
        }
        else
        {
            int separator = FindWhitespace(
                commandLine);
            if (separator < 0)
            {
                fileName = commandLine;
                arguments = string.Empty;
            }
            else
            {
                fileName = commandLine[
                    ..separator];
                arguments = commandLine[
                        separator..]
                    .TrimStart();
            }
        }

        fileName = fileName.Trim();
        if (fileName.Length == 0
            || fileName.Contains('"'))
        {
            return false;
        }

        command = new PanelRunCommand(
            fileName,
            arguments);
        return true;
    }

    private static int FindWhitespace(
        string value)
    {
        for (int index = 0;
             index < value.Length;
             index++)
        {
            if (char.IsWhiteSpace(
                    value[index]))
            {
                return index;
            }
        }

        return -1;
    }
}

internal enum PanelRunStatus
{
    Started,
    Failed
}

internal readonly record struct PanelRunResult(
    PanelRunStatus Status,
    string? Error = null);

internal interface IPanelRunService
{
    Task<PanelRunResult> RunAsync(
        PanelRunCommand command);
}

internal sealed class PanelRunService :
    IPanelRunService
{
    private readonly Func<string, string>
        _expandEnvironmentVariables;
    private readonly Func<ProcessStartInfo, bool>
        _start;

    internal PanelRunService(
        Func<string, string>?
            expandEnvironmentVariables = null,
        Func<ProcessStartInfo, bool>? start = null)
    {
        _expandEnvironmentVariables =
            expandEnvironmentVariables
            ?? Environment
                .ExpandEnvironmentVariables;
        _start = start ?? Start;
    }

    public Task<PanelRunResult> RunAsync(
        PanelRunCommand command) =>
        Task.Run(() => Run(command));

    private PanelRunResult Run(
        PanelRunCommand command)
    {
        try
        {
            ProcessStartInfo request =
                BuildRequest(
                    command,
                    _expandEnvironmentVariables);
            return _start(request)
                ? new PanelRunResult(
                    PanelRunStatus.Started)
                : new PanelRunResult(
                    PanelRunStatus.Failed);
        }
        catch (Exception exception)
        {
            return new PanelRunResult(
                PanelRunStatus.Failed,
                exception.Message);
        }
    }

    internal static ProcessStartInfo BuildRequest(
        PanelRunCommand command,
        Func<string, string> expand)
    {
        ArgumentNullException.ThrowIfNull(expand);
        string fileName = expand(
                command.FileName)
            .Trim();
        if (fileName.Length == 0)
        {
            throw new ArgumentException(
                "运行目标不能为空。",
                nameof(command));
        }

        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = expand(
                command.Arguments),
            UseShellExecute = true
        };
    }

    private static bool Start(
        ProcessStartInfo request)
    {
        Process.Start(request);
        return true;
    }
}
