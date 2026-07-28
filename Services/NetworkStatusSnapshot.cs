namespace FocusPanel.Services;

public enum NetworkConnectionKind
{
    Unknown,
    WiFi,
    Ethernet,
    Other
}

public readonly record struct NetworkStatusSnapshot(
    bool IsAvailable,
    NetworkConnectionKind ConnectionKind,
    string DisplayName,
    string Detail)
{
    public static NetworkStatusSnapshot Unavailable { get; } =
        new(
            false,
            NetworkConnectionKind.Unknown,
            "未连接",
            "当前没有可用连接");

    internal static NetworkStatusSnapshot FromObservation(
        bool isAvailable,
        string? displayName,
        NetworkConnectionKind connectionKind,
        string? connectionKindLabel,
        string? ipv4Address)
    {
        if (!isAvailable)
            return Unavailable;

        string name = FirstNonEmpty(
            displayName,
            connectionKindLabel,
            "网络已连接");
        string kindLabel = Normalize(
            connectionKindLabel);
        string ipv4 = Normalize(ipv4Address);
        string detail =
            (kindLabel.Length, ipv4.Length) switch
        {
            (> 0, > 0) =>
                $"{kindLabel} · {ipv4}",
            (> 0, 0) => kindLabel,
            (0, > 0) => ipv4,
            _ => "网络已连接"
        };
        return new NetworkStatusSnapshot(
            true,
            connectionKind,
            name,
            detail);
    }

    private static string FirstNonEmpty(
        string? first,
        string? second,
        string fallback)
    {
        string normalizedFirst = Normalize(first);
        if (normalizedFirst.Length > 0)
            return normalizedFirst;

        string normalizedSecond = Normalize(second);
        return normalizedSecond.Length > 0
            ? normalizedSecond
            : fallback;
    }

    private static string Normalize(string? value)
        => value?.Trim() ?? string.Empty;
}
