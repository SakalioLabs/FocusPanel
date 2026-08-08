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
            "快速搜索快捷键注册失败；请使用所选屏幕边缘热区");
}

internal static class ShellSummonHotkeyPolicy
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;
    private const uint VkF = 0x46;
    private const uint VkW = 0x57;

    internal static IReadOnlyList<
        ShellHotkeyCandidate> SearchCandidates
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

    internal static IReadOnlyList<
        ShellHotkeyCandidate>
        WindowOverviewCandidates
    {
        get;
    } = new[]
    {
        new ShellHotkeyCandidate(
            ModControl
            | ModAlt
            | ModNoRepeat,
            VkW,
            "窗口总览：Ctrl + Alt + W"),
        new ShellHotkeyCandidate(
            ModControl
            | ModShift
            | ModNoRepeat,
            VkW,
            "窗口总览：Ctrl + Shift + W（备用）")
    };

    internal static IReadOnlyList<
        ShellHotkeyCandidate>
        WindowFocusCandidates
    {
        get;
    } = new[]
    {
        new ShellHotkeyCandidate(
            ModControl
            | ModAlt
            | ModNoRepeat,
            VkF,
            "窗口专注：Ctrl + Alt + F"),
        new ShellHotkeyCandidate(
            ModControl
            | ModAlt
            | ModShift
            | ModNoRepeat,
            VkF,
            "窗口专注：Ctrl + Alt + Shift + F（备用）")
    };

    internal static IReadOnlyList<
        ShellHotkeyCandidate> Candidates =>
            SearchCandidates;

    internal static ShellHotkeyRegistration
        RegisterFirstAvailable(
            Func<uint, uint, bool> register)
        => RegisterFirstAvailable(
            SearchCandidates,
            register,
            ShellHotkeyRegistration.Unavailable);

    internal static ShellHotkeyRegistration
        RegisterWindowOverview(
            Func<uint, uint, bool> register) =>
        RegisterFirstAvailable(
            WindowOverviewCandidates,
            register,
            new ShellHotkeyRegistration(
                false,
                "窗口总览快捷键注册失败；仍可点击紧凑栏“窗口”"));

    internal static ShellHotkeyRegistration
        RegisterWindowFocus(
            Func<uint, uint, bool> register) =>
        RegisterFirstAvailable(
            WindowFocusCandidates,
            register,
            new ShellHotkeyRegistration(
                false,
                "窗口专注快捷键注册失败；仍可在应用或窗口右键菜单使用"));

    private static ShellHotkeyRegistration
        RegisterFirstAvailable(
            IReadOnlyList<ShellHotkeyCandidate>
                candidates,
            Func<uint, uint, bool> register,
            ShellHotkeyRegistration unavailable)
    {
        if (register == null)
        {
            throw new ArgumentNullException(
                nameof(register));
        }

        foreach (ShellHotkeyCandidate candidate
                 in candidates)
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

        return unavailable;
    }
}
