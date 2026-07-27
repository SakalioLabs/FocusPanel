using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppIdentityResolverTests
{
    [Fact]
    public void WindowIdentity_PrefersExplicitWindowAumid()
    {
        var native = new FakeNative
        {
            WindowAumid = "Contoso.Window",
            ProcessAumid = "Contoso.Process"
        };
        var resolver = new AppIdentityResolver(native);

        ResolvedAppIdentity identity = resolver.ResolveWindow(
            new IntPtr(10),
            20,
            @"C:\Apps\Contoso.exe");

        Assert.Equal("aumid:contoso.window", identity.Key);
        Assert.Equal(0, native.ProcessAumidReads);
    }

    [Fact]
    public void WindowIdentity_FallsBackFromProcessAumidToExecutable()
    {
        var processResolver = new AppIdentityResolver(new FakeNative
        {
            ProcessAumid = "Contoso.Process"
        });
        var executableResolver = new AppIdentityResolver(new FakeNative());

        Assert.Equal(
            "aumid:contoso.process",
            processResolver.ResolveWindow(IntPtr.Zero, 20, @"C:\Apps\Contoso.exe").Key);
        Assert.Equal(
            @"exe:c:\apps\contoso.exe",
            executableResolver.ResolveWindow(IntPtr.Zero, 20, @"C:\Apps\Contoso.exe").Key);
    }

    [Fact]
    public void ShortcutIdentity_PrefersAumidThenResolvedExecutable()
    {
        var withAumid = new AppIdentityResolver(new FakeNative
        {
            Shortcut = new ShortcutIdentity("Contoso.App", @"C:\Apps\Contoso.exe")
        });
        var withPath = new AppIdentityResolver(new FakeNative
        {
            Shortcut = new ShortcutIdentity(null, @"C:\Apps\Contoso.exe")
        });
        var app = new AppLaunchItem
        {
            LaunchKind = AppLaunchKind.Shortcut,
            LaunchTarget = @"C:\Menu\Contoso.lnk"
        };

        Assert.Equal("aumid:contoso.app", withAumid.ResolveLaunch(app).Key);
        Assert.Equal(@"exe:c:\apps\contoso.exe", withPath.ResolveLaunch(app).Key);
    }

    [Fact]
    public void ExecutableIdentity_NormalizesQuotesSegmentsAndCase()
    {
        var resolver = new AppIdentityResolver(new FakeNative());
        var app = new AppLaunchItem
        {
            LaunchKind = AppLaunchKind.Executable,
            LaunchTarget = "\"C:\\Apps\\Tools\\..\\Contoso.exe\""
        };

        Assert.Equal(@"exe:c:\apps\contoso.exe", resolver.ResolveLaunch(app).Key);
    }

    [Fact]
    public void PackagedApp_UsesAppsFolderAumidDirectly()
    {
        var resolver = new AppIdentityResolver(new FakeNative());
        var app = new AppLaunchItem
        {
            LaunchKind = AppLaunchKind.ShellApp,
            LaunchTarget = "Contoso.Package_123!App"
        };

        Assert.Equal("aumid:contoso.package_123!app", resolver.ResolveLaunch(app).Key);
    }

    [Fact]
    public void UnresolvedWindows_AreIsolatedByProcessInsteadOfDisplayName()
    {
        var resolver = new AppIdentityResolver(new FakeNative());

        Assert.Equal("window:41", resolver.ResolveWindow(IntPtr.Zero, 41, null).Key);
        Assert.Equal("window:42", resolver.ResolveWindow(IntPtr.Zero, 42, null).Key);
    }

    private sealed class FakeNative : IAppIdentityNative
    {
        internal string? WindowAumid { get; init; }
        internal string? ProcessAumid { get; init; }
        internal ShortcutIdentity Shortcut { get; init; }
        internal int ProcessAumidReads { get; private set; }

        public string? GetWindowApplicationUserModelId(IntPtr window) => WindowAumid;

        public string? GetProcessApplicationUserModelId(uint processId)
        {
            ProcessAumidReads++;
            return ProcessAumid;
        }

        public ShortcutIdentity ResolveShortcut(string shortcutPath) => Shortcut;
    }
}
