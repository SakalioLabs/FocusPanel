namespace FocusPanel.Services;

internal readonly record struct NetworkStatusPresentation(
    string Glyph,
    string Summary);

internal static class NetworkStatusPresentationComposer
{
    internal const string WiFiGlyph = "\uE701";
    internal const string EthernetGlyph = "\uE839";
    internal const string GenericNetworkGlyph = "\uE774";
    internal const string DisconnectedGlyph = "\uE783";

    internal static NetworkStatusPresentation Compose(
        bool isAvailable,
        NetworkConnectionKind connectionKind,
        string? displayName)
        => new(
            GetGlyph(isAvailable, connectionKind),
            SystemStatusSummaryComposer.ComposeNetwork(
                isAvailable,
                displayName));

    private static string GetGlyph(
        bool isAvailable,
        NetworkConnectionKind connectionKind)
    {
        if (!isAvailable)
            return DisconnectedGlyph;

        return connectionKind switch
        {
            NetworkConnectionKind.WiFi =>
                WiFiGlyph,
            NetworkConnectionKind.Ethernet =>
                EthernetGlyph,
            _ => GenericNetworkGlyph
        };
    }
}
