using System.Collections.Generic;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellSummonHotkeyPolicyTests
{
    [Fact]
    public void Registration_UsesPrimaryChordFirst()
    {
        var attempts =
            new List<(uint Modifiers, uint Key)>();

        ShellHotkeyRegistration result =
            ShellSummonHotkeyPolicy
                .RegisterFirstAvailable(
                    (modifiers, key) =>
                    {
                        attempts.Add(
                            (modifiers, key));
                        return true;
                    });

        Assert.True(result.IsRegistered);
        Assert.Contains(
            "Ctrl + Alt + Space",
            result.DisplayText);
        Assert.Contains(
            "快速搜索",
            result.DisplayText);
        Assert.Single(attempts);
        Assert.NotEqual(
            0u,
            attempts[0].Modifiers
                & 0x4000u);
    }

    [Fact]
    public void Registration_FallsBackWhenPrimaryIsTaken()
    {
        int attempts = 0;

        ShellHotkeyRegistration result =
            ShellSummonHotkeyPolicy
                .RegisterFirstAvailable(
                    (_, _) => ++attempts == 2);

        Assert.True(result.IsRegistered);
        Assert.Equal(2, attempts);
        Assert.Contains(
            "Ctrl + Shift + Space",
            result.DisplayText);
        Assert.Contains(
            "备用",
            result.DisplayText);
    }

    [Fact]
    public void Registration_ReportsUnavailableAfterAllCandidatesFail()
    {
        int attempts = 0;

        ShellHotkeyRegistration result =
            ShellSummonHotkeyPolicy
                .RegisterFirstAvailable(
                    (_, _) =>
                    {
                        attempts++;
                        return false;
                    });

        Assert.False(result.IsRegistered);
        Assert.Equal(
            ShellSummonHotkeyPolicy
                .Candidates.Count,
            attempts);
        Assert.Contains(
            "所选屏幕边缘热区",
            result.DisplayText);
        Assert.Contains(
            "快速搜索",
            result.DisplayText);
    }

    [Fact]
    public void Registration_ContinuesWhenNativeBoundaryThrows()
    {
        int attempts = 0;

        ShellHotkeyRegistration result =
            ShellSummonHotkeyPolicy
                .RegisterFirstAvailable(
                    (_, _) =>
                    {
                        attempts++;
                        if (attempts == 1)
                            throw new System.ComponentModel.Win32Exception();
                        return true;
                    });

        Assert.True(result.IsRegistered);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void WindowOverview_UsesDedicatedPrimaryChord()
    {
        var attempts =
            new List<(uint Modifiers, uint Key)>();

        ShellHotkeyRegistration result =
            ShellSummonHotkeyPolicy
                .RegisterWindowOverview(
                    (modifiers, key) =>
                    {
                        attempts.Add(
                            (modifiers, key));
                        return true;
                    });

        Assert.True(result.IsRegistered);
        Assert.Contains(
            "Ctrl + Alt + W",
            result.DisplayText);
        Assert.Contains(
            "窗口总览",
            result.DisplayText);
        Assert.Single(attempts);
        Assert.Equal(0x57u, attempts[0].Key);
        Assert.NotEqual(
            0u,
            attempts[0].Modifiers
                & 0x4000u);
    }

    [Fact]
    public void WindowOverview_FallsBackAndReportsVisibleEntry()
    {
        int attempts = 0;
        ShellHotkeyRegistration fallback =
            ShellSummonHotkeyPolicy
                .RegisterWindowOverview(
                    (_, _) => ++attempts == 2);

        Assert.True(fallback.IsRegistered);
        Assert.Contains(
            "Ctrl + Shift + W",
            fallback.DisplayText);
        Assert.Equal(2, attempts);

        ShellHotkeyRegistration unavailable =
            ShellSummonHotkeyPolicy
                .RegisterWindowOverview(
                    (_, _) => false);

        Assert.False(unavailable.IsRegistered);
        Assert.Contains(
            "紧凑栏“窗口”",
            unavailable.DisplayText);
    }
}
