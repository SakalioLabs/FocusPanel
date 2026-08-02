using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

internal readonly record struct ShellHotkeyCandidate(
    uint Modifiers,
    uint VirtualKey,
    string DisplayText);

internal readonly record struct ShellHotkeyRegistration(
    bool IsRegistered,
    string DisplayText)
{
    internal static ShellHotkeyRegistration
        Unavailable { get; } =
        new(
            false,
            "快速搜索快捷键注册失败；请使用右缘热区");
}

internal static class ShellSummonHotkeyPolicy
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;

    internal static IReadOnlyList<
        ShellHotkeyCandidate> Candidates
    {
        get;
    } = new[]
    {
        new ShellHotkeyCandidate(
            ModControl
            | ModAlt
            | ModNoRepeat,
            VkSpace,
            "快速搜索：Ctrl + Alt + Space"),
        new ShellHotkeyCandidate(
            ModControl
            | ModShift
            | ModNoRepeat,
            VkSpace,
            "快速搜索：Ctrl + Shift + Space（备用）")
    };

    internal static ShellHotkeyRegistration
        RegisterFirstAvailable(
            Func<uint, uint, bool> register)
    {
        if (register == null)
        {
            throw new ArgumentNullException(
                nameof(register));
        }

        foreach (ShellHotkeyCandidate candidate
                 in Candidates)
        {
            try
            {
                if (register(
                        candidate.Modifiers,
                        candidate.VirtualKey))
                {
                    return new ShellHotkeyRegistration(
                        true,
                        candidate.DisplayText);
                }
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // One unavailable chord must not prevent the
                // later fallback from being attempted.
            }
        }

        return ShellHotkeyRegistration
            .Unavailable;
    }
}
