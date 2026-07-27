namespace FocusPanel.Services;

public enum TaskbarReplacementStopReason
{
    WindowsTaskbarReappeared,
    ExplorerHostChanged,
    EmergencyRestore,
    StartupFailure,
    Unknown
}

public sealed record TaskbarReplacementStoppedEvent(
    TaskbarReplacementStopReason Reason,
    string Message);
