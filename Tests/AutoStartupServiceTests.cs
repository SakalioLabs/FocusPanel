using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AutoStartupServiceTests
{
    [Fact]
    public void EnableWritesQuotedExecutablePath()
    {
        var registry = new FakeAutoStartupRegistry();

        bool succeeded = AutoStartupService.TrySetStartup(
            enable: true,
            registry,
            @"C:\Program Files\FocusPanel\FocusPanel.exe",
            out string? error);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Equal(
            "\"C:\\Program Files\\FocusPanel\\FocusPanel.exe\"",
            registry.Command);
        Assert.False(registry.DeleteCalled);
    }

    [Fact]
    public void DisableDeletesValueWithoutExecutablePath()
    {
        var registry = new FakeAutoStartupRegistry
        {
            Command = "\"old.exe\""
        };

        bool succeeded = AutoStartupService.TrySetStartup(
            enable: false,
            registry,
            executablePath: null,
            out string? error);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.True(registry.DeleteCalled);
        Assert.Null(registry.Command);
    }

    [Fact]
    public void EnableWithoutExecutablePathDoesNotClaimSuccess()
    {
        var registry = new FakeAutoStartupRegistry();

        bool succeeded = AutoStartupService.TrySetStartup(
            enable: true,
            registry,
            executablePath: " ",
            out string? error);

        Assert.False(succeeded);
        Assert.Contains("无法定位", error);
        Assert.Null(registry.Command);
    }

    [Fact]
    public void RegistryFailureReturnsActionableError()
    {
        var registry = new FakeAutoStartupRegistry
        {
            Failure = new UnauthorizedAccessException("访问被拒绝")
        };

        bool succeeded = AutoStartupService.TrySetStartup(
            enable: true,
            registry,
            @"C:\FocusPanel.exe",
            out string? error);

        Assert.False(succeeded);
        Assert.Contains("访问被拒绝", error);
    }

    [Fact]
    public void StartupStateRequiresNonBlankRegistryCommand()
    {
        var registry = new FakeAutoStartupRegistry();
        Assert.False(AutoStartupService.IsStartupEnabled(registry));

        registry.Command = " ";
        Assert.False(AutoStartupService.IsStartupEnabled(registry));

        registry.Command = "\"C:\\FocusPanel.exe\"";
        Assert.True(AutoStartupService.IsStartupEnabled(registry));
    }

    [Fact]
    public void RegistryReadFailureSafelyReportsDisabled()
    {
        var registry = new FakeAutoStartupRegistry
        {
            Failure = new InvalidOperationException("注册表不可用")
        };

        Assert.False(
            AutoStartupService.IsStartupEnabled(registry));
    }

    private sealed class FakeAutoStartupRegistry
        : IAutoStartupRegistry
    {
        public string? Command { get; set; }
        public bool DeleteCalled { get; private set; }
        public Exception? Failure { get; set; }

        public string? ReadCommand()
        {
            ThrowIfNeeded();
            return Command;
        }

        public void WriteCommand(string command)
        {
            ThrowIfNeeded();
            Command = command;
        }

        public void DeleteCommand()
        {
            ThrowIfNeeded();
            DeleteCalled = true;
            Command = null;
        }

        private void ThrowIfNeeded()
        {
            if (Failure != null)
                throw Failure;
        }
    }
}
