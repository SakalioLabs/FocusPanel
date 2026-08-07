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
        "显示隐藏图标",
        "Show hidden icons",
        "Show hidden icon",
        "隠れているインジケーターを表示します",
        "숨겨진 아이콘 표시"
    };

    private static readonly string[] RejectedNames =
    {
        "输入指示",
        "輸入指示",
        "Input indicator",
        "入力インジケーター",
        "Network",
        "网络",
        "網路",
        "Volume",
        "音量",
        "Battery",
        "电池",
        "電池",
        "Clock",
        "时钟",
        "時鐘"
    };

    public static int FindBestCandidate(
        IReadOnlyList<TrayAutomationNode> nodes)
    {
        int namedIndex = -1;
        int namedScore = 0;
        int shapeIndex = -1;
        int shapeCount = 0;

        for (int index = 0; index < nodes.Count; index++)
        {
            TrayAutomationNode node = nodes[index];
            if (!node.CanInvoke
                || ContainsAny(node.Name, RejectedNames))
            {
                continue;
            }

            bool hasKnownName = ContainsAny(
                node.Name,
                OverflowNames);
            bool hasTrayAutomationId = string.Equals(
                    node.AutomationId,
                    "SystemTrayIcon",
                    StringComparison.Ordinal);
            bool hasNormalButtonClass = string.Equals(
                    node.ClassName,
                    "SystemTray.NormalButton",
                    StringComparison.Ordinal);

            if (hasKnownName)
            {
                int score = 100
                    + (hasTrayAutomationId
                        ? 10
                        : 0)
                    + (hasNormalButtonClass
                        ? 10
                        : 0);
                if (score > namedScore)
                {
                    namedIndex = index;
                    namedScore = score;
                }
            }
            else if (hasTrayAutomationId
                     && hasNormalButtonClass)
            {
                shapeIndex = index;
                shapeCount++;
            }
        }

        if (namedIndex >= 0)
            return namedIndex;

        return shapeCount == 1
            ? shapeIndex
            : -1;
    }

    private static bool ContainsAny(
        string value,
        IEnumerable<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            if (value.Contains(
                    candidate,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
