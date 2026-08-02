using System;
using Xunit;

namespace FocusPanel.Tests;

/// <summary>
/// Runs visual process checks on an interactive Windows desktop while making
/// the non-interactive GitHub Actions limitation explicit in test results.
/// </summary>
public sealed class InteractiveWindowsFactAttribute
    : FactAttribute
{
    public InteractiveWindowsFactAttribute()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable(
                    "GITHUB_ACTIONS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip =
                "GitHub Windows Runner 没有可交互桌面；"
                + "该 WPF 视觉冒烟由发布前本机验证执行。";
        }
    }
}
