using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

public sealed record TrayAutomationNode(
    string Name,
    string AutomationId,
    string ClassName,
    bool CanInvoke);

public static class TrayOverflowTargetSelector
{
    private static readonly string[] OverflowNames =
    {
        "显示隐藏的图标",
        "顯示隱藏的圖示",
        "Show hidden icons",
        "隠れているインジケーターを表示します"
    };

    private static readonly string[] InputIndicatorNames =
    {
        "输入指示",
        "輸入指示",
        "Input indicator",
        "入力インジケーター"
    };

    public static int FindBestCandidate(IReadOnlyList<TrayAutomationNode> nodes)
    {
        int bestIndex = -1;
        int bestScore = 0;

        for (int index = 0; index < nodes.Count; index++)
        {
            TrayAutomationNode node = nodes[index];
            if (!node.CanInvoke || ContainsAny(node.Name, InputIndicatorNames))
                continue;

            int score = ContainsAny(node.Name, OverflowNames) ? 100 : 0;
            if (string.Equals(node.AutomationId, "SystemTrayIcon", StringComparison.Ordinal))
                score += 10;
            if (string.Equals(node.ClassName, "SystemTray.NormalButton", StringComparison.Ordinal))
                score += 10;

            if (score > bestScore)
            {
                bestIndex = index;
                bestScore = score;
            }
        }

        return bestScore >= 20 ? bestIndex : -1;
    }

    private static bool ContainsAny(string value, IEnumerable<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
