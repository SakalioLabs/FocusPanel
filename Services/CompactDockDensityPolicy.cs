using System;

namespace FocusPanel.Services;

internal static class CompactDockDensityPolicy
{
    internal const double NormalEntryHeightDip = 54;
    internal const double DenseEntryHeightDip = 44;
    internal const double DenseHeightThresholdDip = 640;

    internal static double GetEntryHeight(
        double panelHeightDip)
    {
        if (!double.IsFinite(panelHeightDip)
            || panelHeightDip <= 0)
        {
            return NormalEntryHeightDip;
        }

        return panelHeightDip
               < DenseHeightThresholdDip
            ? DenseEntryHeightDip
            : NormalEntryHeightDip;
    }

    internal static bool UsesCombinedFocusEntry(
        double panelHeightDip) =>
        double.IsFinite(panelHeightDip)
        && panelHeightDip > 0
        && panelHeightDip
           < DenseHeightThresholdDip;
}
