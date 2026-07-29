using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

internal enum TaskbarSlotHotkeyAction
{
    ActivateOrLaunch,
    LaunchNewInstance
}

internal readonly record struct
    TaskbarSlotHotkeyBinding(
        int Id,
        int SlotIndex,
        TaskbarSlotHotkeyAction Action,
        uint Modifiers,
        uint VirtualKey)
{
    internal int SlotNumber =>
        SlotIndex + 1;
}

internal readonly record struct
    TaskbarSlotHotkeyRegistration(
        IReadOnlyList<
            TaskbarSlotHotkeyBinding>
            Bindings)
{
    internal int ActivationCount =>
        Bindings.Count(binding =>
            binding.Action
            == TaskbarSlotHotkeyAction
                .ActivateOrLaunch);

    internal int NewInstanceCount =>
        Bindings.Count(binding =>
            binding.Action
            == TaskbarSlotHotkeyAction
                .LaunchNewInstance);

    internal string DisplayText
    {
        get
        {
            if (ActivationCount == 0
                && NewInstanceCount == 0)
            {
                return "快速应用快捷键注册失败；"
                       + "Ctrl + Alt + 数字键可能已被其他程序占用";
            }

            if (ActivationCount
                    == TaskbarSlotHotkeyPolicy
                        .SlotCount
                && NewInstanceCount
                    == TaskbarSlotHotkeyPolicy
                        .SlotCount)
            {
                return "快速应用：Ctrl + Alt + 1…9 "
                       + "启动或切换；加 Shift 启动新实例";
            }

            string activationSlots =
                FormatSlots(
                    TaskbarSlotHotkeyAction
                        .ActivateOrLaunch);
            string newInstanceSlots =
                FormatSlots(
                    TaskbarSlotHotkeyAction
                        .LaunchNewInstance);
            return "快速应用（Ctrl + Alt + 数字）："
                   + $"启动/切换 {activationSlots}，"
                   + $"加 Shift 新实例 {newInstanceSlots}；"
                   + "未注册组合已留给其他程序";
        }
    }

    private string FormatSlots(
        TaskbarSlotHotkeyAction action)
    {
        int[] slots =
            Bindings
                .Where(binding =>
                    binding.Action
                    == action)
                .Select(binding =>
                    binding.SlotNumber)
                .OrderBy(slot => slot)
                .ToArray();
        if (slots.Length == 0)
            return "无";

        var ranges =
            new List<string>();
        int rangeStart = slots[0];
        int rangeEnd = slots[0];
        for (int index = 1;
             index < slots.Length;
             index++)
        {
            if (slots[index]
                == rangeEnd + 1)
            {
                rangeEnd =
                    slots[index];
                continue;
            }

            ranges.Add(
                FormatRange(
                    rangeStart,
                    rangeEnd));
            rangeStart =
                slots[index];
            rangeEnd =
                slots[index];
        }

        ranges.Add(
            FormatRange(
                rangeStart,
                rangeEnd));
        return string.Join(
            "、",
            ranges);
    }

    private static string FormatRange(
        int start,
        int end) =>
        start == end
            ? start.ToString()
            : $"{start}–{end}";
}

internal enum TaskbarSlotInvocationKind
{
    None,
    ActivateOrLaunch,
    LaunchNewInstance
}

internal static class
    TaskbarSlotHotkeyPolicy
{
    internal const int SlotCount = 9;
    internal const int ActivationIdBase =
        0x4700;
    internal const int NewInstanceIdBase =
        0x4710;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkDigit1 = 0x31;

    internal static IReadOnlyList<
        TaskbarSlotHotkeyBinding> Bindings
    {
        get;
    } = CreateBindings();

    internal static TaskbarSlotInvocationKind
        GetInvocation(
            int taskbarAppCount,
            TaskbarSlotHotkeyBinding binding,
            bool canLaunchNewInstance)
    {
        if (binding.SlotIndex < 0
            || binding.SlotIndex
                >= taskbarAppCount)
        {
            return TaskbarSlotInvocationKind
                .None;
        }

        return binding.Action
            switch
            {
                TaskbarSlotHotkeyAction
                    .ActivateOrLaunch =>
                    TaskbarSlotInvocationKind
                        .ActivateOrLaunch,
                TaskbarSlotHotkeyAction
                    .LaunchNewInstance
                    when canLaunchNewInstance =>
                    TaskbarSlotInvocationKind
                        .LaunchNewInstance,
                _ =>
                    TaskbarSlotInvocationKind
                        .None
            };
    }

    private static IReadOnlyList<
        TaskbarSlotHotkeyBinding>
        CreateBindings()
    {
        var bindings =
            new List<
                TaskbarSlotHotkeyBinding>(
                SlotCount * 2);
        for (int slotIndex = 0;
             slotIndex < SlotCount;
             slotIndex++)
        {
            uint virtualKey =
                VkDigit1
                + (uint)slotIndex;
            bindings.Add(
                new TaskbarSlotHotkeyBinding(
                    ActivationIdBase
                    + slotIndex,
                    slotIndex,
                    TaskbarSlotHotkeyAction
                        .ActivateOrLaunch,
                    ModControl
                    | ModAlt
                    | ModNoRepeat,
                    virtualKey));
            bindings.Add(
                new TaskbarSlotHotkeyBinding(
                    NewInstanceIdBase
                    + slotIndex,
                    slotIndex,
                    TaskbarSlotHotkeyAction
                        .LaunchNewInstance,
                    ModControl
                    | ModAlt
                    | ModShift
                    | ModNoRepeat,
                    virtualKey));
        }

        return bindings;
    }
}

internal sealed class
    TaskbarSlotHotkeySession :
        IDisposable
{
    private readonly Func<
        TaskbarSlotHotkeyBinding,
        bool> _register;
    private readonly Action<int> _unregister;
    private readonly Dictionary<
        int,
        TaskbarSlotHotkeyBinding>
        _registered = new();
    private bool _registrationAttempted;
    private bool _disposed;

    internal TaskbarSlotHotkeySession(
        Func<TaskbarSlotHotkeyBinding, bool>
            register,
        Action<int> unregister)
    {
        _register =
            register
            ?? throw new ArgumentNullException(
                nameof(register));
        _unregister =
            unregister
            ?? throw new ArgumentNullException(
                nameof(unregister));
    }

    internal TaskbarSlotHotkeyRegistration
        RegisterAvailable()
    {
        if (_disposed)
        {
            return new
                TaskbarSlotHotkeyRegistration(
                    Array.Empty<
                        TaskbarSlotHotkeyBinding>());
        }

        if (_registrationAttempted)
            return GetRegistration();

        _registrationAttempted = true;
        foreach (TaskbarSlotHotkeyBinding
                 binding
                 in TaskbarSlotHotkeyPolicy
                     .Bindings)
        {
            try
            {
                if (_register(binding))
                {
                    _registered.Add(
                        binding.Id,
                        binding);
                }
            }
            catch (
                System.Runtime.InteropServices
                    .ExternalException)
            {
                // A conflicting or unavailable chord must
                // not prevent other slots from registering.
            }
        }

        return GetRegistration();
    }

    internal bool TryResolve(
        int hotkeyId,
        out TaskbarSlotHotkeyBinding
            binding) =>
        _registered.TryGetValue(
            hotkeyId,
            out binding);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (int hotkeyId
                 in _registered.Keys)
        {
            try
            {
                _unregister(hotkeyId);
            }
            catch (
                System.Runtime.InteropServices
                    .ExternalException)
            {
                // The owning HWND may already be gone.
            }
        }

        _registered.Clear();
    }

    private TaskbarSlotHotkeyRegistration
        GetRegistration() =>
        new(
            _registered.Values
                .OrderBy(binding =>
                    binding.Id)
                .ToArray());
}
