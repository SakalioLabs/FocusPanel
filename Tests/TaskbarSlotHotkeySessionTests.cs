using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    TaskbarSlotHotkeySessionTests
{
    [Fact]
    public void Bindings_CoverNineSlotsWithUniqueIds()
    {
        IReadOnlyList<
            TaskbarSlotHotkeyBinding>
            bindings =
                TaskbarSlotHotkeyPolicy
                    .Bindings;

        Assert.Equal(18, bindings.Count);
        Assert.Equal(
            18,
            bindings
                .Select(binding =>
                    binding.Id)
                .Distinct()
                .Count());
        for (int slotIndex = 0;
             slotIndex < 9;
             slotIndex++)
        {
            TaskbarSlotHotkeyBinding[]
                slotBindings =
                    bindings
                        .Where(binding =>
                            binding.SlotIndex
                            == slotIndex)
                        .ToArray();
            Assert.Equal(
                2,
                slotBindings.Length);
            Assert.All(
                slotBindings,
                binding =>
                {
                    Assert.Equal(
                        0x31u
                        + (uint)slotIndex,
                        binding.VirtualKey);
                    Assert.NotEqual(
                        0u,
                        binding.Modifiers
                        & 0x4000u);
                });
            Assert.Contains(
                slotBindings,
                binding =>
                    binding.Action
                    == TaskbarSlotHotkeyAction
                        .ActivateOrLaunch
                    && (binding.Modifiers
                        & 0x0004u)
                    == 0);
            Assert.Contains(
                slotBindings,
                binding =>
                    binding.Action
                    == TaskbarSlotHotkeyAction
                        .LaunchNewInstance
                    && (binding.Modifiers
                        & 0x0004u)
                    != 0);
        }
    }

    [Fact]
    public void RegisterAvailable_ContinuesAfterConflicts()
    {
        using var session =
            new TaskbarSlotHotkeySession(
                binding =>
                    binding.SlotIndex
                        % 2
                    == 0,
                _ => { });

        TaskbarSlotHotkeyRegistration
            registration =
                session.RegisterAvailable();

        Assert.Equal(
            5,
            registration.ActivationCount);
        Assert.Equal(
            5,
            registration.NewInstanceCount);
        Assert.Contains(
            "1、3、5、7、9",
            registration.DisplayText);
        Assert.Contains(
            "未注册组合",
            registration.DisplayText);
    }

    [Fact]
    public void RegisterAvailable_ContinuesAfterNativeException()
    {
        int attempts = 0;
        using var session =
            new TaskbarSlotHotkeySession(
                _ =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        throw new
                            System.ComponentModel
                                .Win32Exception();
                    }

                    return true;
                },
                _ => { });

        TaskbarSlotHotkeyRegistration
            registration =
                session.RegisterAvailable();

        Assert.Equal(18, attempts);
        Assert.Equal(
            17,
            registration.Bindings.Count);
    }

    [Fact]
    public void Resolve_OnlyReturnsSuccessfullyRegisteredIds()
    {
        TaskbarSlotHotkeyBinding accepted =
            TaskbarSlotHotkeyPolicy
                .Bindings[4];
        using var session =
            new TaskbarSlotHotkeySession(
                binding =>
                    binding.Id
                    == accepted.Id,
                _ => { });
        session.RegisterAvailable();

        Assert.True(
            session.TryResolve(
                accepted.Id,
                out TaskbarSlotHotkeyBinding
                    resolved));
        Assert.Equal(
            accepted,
            resolved);
        Assert.False(
            session.TryResolve(
                TaskbarSlotHotkeyPolicy
                    .Bindings[5]
                    .Id,
                out _));
    }

    [Fact]
    public void Dispose_UnregistersEachSuccessfulIdExactlyOnce()
    {
        var unregistered =
            new List<int>();
        var session =
            new TaskbarSlotHotkeySession(
                binding =>
                    binding.SlotIndex < 2,
                id =>
                    unregistered.Add(id));
        TaskbarSlotHotkeyRegistration
            registration =
                session.RegisterAvailable();

        session.Dispose();
        session.Dispose();

        Assert.Equal(
            registration.Bindings.Count,
            unregistered.Count);
        Assert.Equal(
            unregistered.Count,
            unregistered.Distinct()
                .Count());
    }

    [Theory]
    [InlineData(
        3,
        2,
        0,
        false,
        1)]
    [InlineData(
        3,
        2,
        1,
        true,
        2)]
    [InlineData(
        3,
        2,
        1,
        false,
        0)]
    [InlineData(
        3,
        3,
        0,
        true,
        0)]
    public void Invocation_ValidatesSlotAndLaunchTarget(
        int appCount,
        int slotIndex,
        int actionValue,
        bool canLaunchNewInstance,
        int expectedValue)
    {
        TaskbarSlotHotkeyAction action =
            (TaskbarSlotHotkeyAction)
                actionValue;
        TaskbarSlotInvocationKind
            expected =
                (TaskbarSlotInvocationKind)
                    expectedValue;
        var binding =
            new TaskbarSlotHotkeyBinding(
                1,
                slotIndex,
                action,
                0,
                0);

        Assert.Equal(
            expected,
            TaskbarSlotHotkeyPolicy
                .GetInvocation(
                    appCount,
                    binding,
                    canLaunchNewInstance));
    }

    [Fact]
    public void EmptyRegistration_ExplainsShortcutConflict()
    {
        using var session =
            new TaskbarSlotHotkeySession(
                _ => false,
                _ => { });

        TaskbarSlotHotkeyRegistration
            registration =
                session.RegisterAvailable();

        Assert.Empty(
            registration.Bindings);
        Assert.Contains(
            "其他程序占用",
            registration.DisplayText);
    }
}
