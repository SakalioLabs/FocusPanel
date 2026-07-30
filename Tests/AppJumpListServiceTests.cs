using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppJumpListServiceTests
{
    [Fact]
    public void Normalize_DeduplicatesBoundsAndCleansTitles()
    {
        AppJumpListItem[] source =
            Enumerable.Range(0, 12)
                .Select(index =>
                            new AppJumpListItem(
                                $"  文档 {index}\r\n副标题  ",
                                $@"C:\Docs\{index}.txt",
                                null)
                            {
                                Source =
                                    index == 0
                                        ? AppJumpListItemSource
                                            .ShellLink
                                        : AppJumpListItemSource
                                            .ShellItem
                            })
                .Append(
                    new AppJumpListItem(
                        "重复",
                        @"c:\docs\0.txt",
                        null))
                .ToArray();

        IReadOnlyList<AppJumpListItem>
            result =
                AppJumpListPolicy
                    .Normalize(
                        source,
                        20);

        Assert.Equal(
            AppJumpListPolicy
                .MaximumItemCount,
            result.Count);
        Assert.Equal(
            "文档 0 副标题",
            result[0].DisplayName);
        Assert.Equal(
            AppJumpListItemSource
                .ShellLink,
            result[0].Source);
        Assert.Equal(
            result.Count,
            result.Select(item =>
                    item.IdentityKey)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .Count());
    }

    [Fact]
    public void Normalize_FallsBackToTargetNameAndSkipsBlankTarget()
    {
        IReadOnlyList<AppJumpListItem>
            result =
                AppJumpListPolicy
                    .Normalize(
                        new[]
                        {
                            new AppJumpListItem(
                                string.Empty,
                                string.Empty,
                                null),
                            new AppJumpListItem(
                                " ",
                                @"C:\Docs\计划.xlsx",
                                null)
                        },
                        8);

        AppJumpListItem item =
            Assert.Single(result);
        Assert.Equal(
            "计划.xlsx",
            item.DisplayName);
    }

    [Fact]
    public void Normalize_TruncatesWithoutSplittingUnicodeTextElement()
    {
        string longTitle =
            string.Concat(
                Enumerable.Repeat(
                    "🙂",
                    120));

        AppJumpListItem item =
            Assert.Single(
                AppJumpListPolicy
                    .Normalize(
                        new[]
                        {
                            new AppJumpListItem(
                                longTitle,
                                @"C:\Docs\a.txt",
                                null)
                        },
                        1));

        Assert.EndsWith(
            "…",
            item.DisplayName);
        Assert.False(
            char.IsHighSurrogate(
                item.DisplayName[^2]));
    }

    [Fact]
    public void ComposeGroups_BalancesAndDeduplicatesCategories()
    {
        AppJumpListItem[] recent =
            Enumerable.Range(0, 7)
                .Select(index =>
                    new AppJumpListItem(
                        $"最近 {index}",
                        $@"C:\Docs\{index}.txt",
                        null))
                .ToArray();
        AppJumpListItem[] frequent =
            new[]
            {
                new AppJumpListItem(
                    "重复",
                    @"c:\docs\0.txt",
                    null)
            }
            .Concat(
                Enumerable.Range(7, 6)
                    .Select(index =>
                        new AppJumpListItem(
                            $"常用 {index}",
                            $@"C:\Docs\{index}.txt",
                            null)))
            .ToArray();

        IReadOnlyList<AppJumpListGroup>
            groups =
                AppJumpListPolicy
                    .ComposeGroups(
                        recent,
                        frequent,
                        8);

        Assert.Equal(2, groups.Count);
        Assert.Equal(
            AppJumpListCategory.Recent,
            groups[0].Category);
        Assert.Equal(
            AppJumpListCategory.Frequent,
            groups[1].Category);
        Assert.Equal(4, groups[0].Items.Count);
        Assert.Equal(4, groups[1].Items.Count);
        Assert.Equal(
            8,
            groups.SelectMany(group =>
                    group.Items)
                .Select(item =>
                    item.IdentityKey)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .Count());
    }

    [Fact]
    public void ComposeGroups_FillsCapacityFromAvailableCategory()
    {
        AppJumpListItem[] frequent =
            Enumerable.Range(0, 8)
                .Select(index =>
                    new AppJumpListItem(
                        $"常用 {index}",
                        $@"C:\Docs\{index}.txt",
                        null))
                .ToArray();

        AppJumpListGroup group =
            Assert.Single(
                AppJumpListPolicy
                    .ComposeGroups(
                        Array.Empty<
                            AppJumpListItem>(),
                        frequent,
                        8));

        Assert.Equal(
            AppJumpListCategory.Frequent,
            group.Category);
        Assert.Equal(8, group.Items.Count);
    }

    [Fact]
    public async Task Read_RunsOnStaAndUsesExplicitAppId()
    {
        var native =
            new FakeJumpListNative();
        using var service =
            new AppJumpListService(
                native,
                _ => true);

        IReadOnlyList<AppJumpListGroup>
            result =
                await service
                    .GetDestinationsAsync(
                        "Demo.Editor",
                        8);

        Assert.Single(result);
        Assert.Equal(
            new[]
            {
                AppJumpListCategory.Recent,
                AppJumpListCategory.Frequent
            },
            native.ObservedCategories);
        Assert.Equal(
            "Demo.Editor",
            native.ObservedAppId);
        Assert.Equal(
            ApartmentState.STA,
            native.ObservedApartment);
        Assert.NotEqual(
            Environment
                .CurrentManagedThreadId,
            native.ObservedThreadId);
    }

    [Fact]
    public async Task Read_NativeFailureReturnsEmpty()
    {
        using var service =
            new AppJumpListService(
                new FakeJumpListNative
                {
                    ThrowOnRead = true
                },
                _ => true);

        IReadOnlyList<AppJumpListGroup>
            result =
                await service
                    .GetDestinationsAsync(
                        "Demo.Editor",
                        8);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Read_OneCategoryFailureKeepsOtherCategory()
    {
        using var service =
            new AppJumpListService(
                new FakeJumpListNative
                {
                    ThrowCategory =
                        AppJumpListCategory
                            .Recent
                },
                _ => true);

        AppJumpListGroup group =
            Assert.Single(
                await service
                    .GetDestinationsAsync(
                        "Demo.Editor",
                        8));

        Assert.Equal(
            AppJumpListCategory.Frequent,
            group.Category);
        Assert.Single(group.Items);
    }

    [Fact]
    public async Task Read_CancelledRequestDoesNotCallNative()
    {
        var native =
            new FakeJumpListNative();
        using var service =
            new AppJumpListService(
                native,
                _ => true);
        using var cancellation =
            new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            async () =>
                await service
                    .GetDestinationsAsync(
                        "Demo.Editor",
                        8,
                        cancellation.Token));
        Assert.Equal(
            0,
            native.ReadCount);
    }

    [Fact]
    public async Task Read_CancellationDropsInFlightNativeResult()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        var native =
            new BlockingJumpListNative(
                started,
                release);
        using var service =
            new AppJumpListService(
                native,
                _ => true);
        using var cancellation =
            new CancellationTokenSource();

        Task<IReadOnlyList<
            AppJumpListGroup>> request =
                service.GetDestinationsAsync(
                    "Demo.Editor",
                    8,
                    cancellation.Token);
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        cancellation.Cancel();
        release.Set();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            async () =>
                await request);
    }

    [Fact]
    public async Task Open_UsesShellExecutionOffCallerThread()
    {
        ProcessStartInfo? observed =
            null;
        int callerThread =
            Environment
                .CurrentManagedThreadId;
        int launchThread =
            callerThread;
        using var service =
            new AppJumpListService(
                new FakeJumpListNative(),
                request =>
                {
                    observed = request;
                    launchThread =
                        Environment
                            .CurrentManagedThreadId;
                    return true;
                });

        bool result =
            await service.OpenAsync(
                new AppJumpListItem(
                    "计划",
                    @"C:\Apps\Editor.exe",
                    "\"C:\\Docs\\计划.txt\"")
                {
                    Source =
                        AppJumpListItemSource
                            .ShellLink
                },
                null);

        Assert.True(result);
        Assert.NotNull(observed);
        Assert.True(
            observed!.UseShellExecute);
        Assert.Equal(
            @"C:\Apps\Editor.exe",
            observed.FileName);
        Assert.Equal(
            "\"C:\\Docs\\计划.txt\"",
            observed.Arguments);
        Assert.NotEqual(
            callerThread,
            launchThread);
    }

    [Fact]
    public async Task DisposedServiceRejectsReadAndOpen()
    {
        var native =
            new FakeJumpListNative();
        var service =
            new AppJumpListService(
                native,
                _ => true);
        service.Dispose();

        IReadOnlyList<AppJumpListGroup>
            destinations =
                await service
                    .GetDestinationsAsync(
                        "Demo.Editor",
                        8);
        bool opened =
            await service.OpenAsync(
                new AppJumpListItem(
                    "文档",
                    @"C:\Docs\a.txt",
                    null),
                null);

        Assert.Empty(destinations);
        Assert.False(opened);
        Assert.Equal(
            0,
            native.ReadCount);
    }

    [Fact]
    public async Task Open_StartFailureIsContained()
    {
        using var service =
            new AppJumpListService(
                new FakeJumpListNative(),
                _ =>
                    throw new
                        InvalidOperationException(
                            "shell rejected"));

        bool opened =
            await service.OpenAsync(
                new AppJumpListItem(
                    "文档",
                    @"C:\Docs\a.txt",
                    null),
                null);

        Assert.False(opened);
    }

    [Fact]
    public void OpenRequest_ShellItemTargetsOriginalDesktopApplication()
    {
        var item =
            new AppJumpListItem(
                "计划",
                @"C:\Docs\计划.txt",
                null);
        var application =
            new AppJumpListApplicationLaunch(
                AppLaunchKind.Executable,
                @"C:\Apps\Editor.exe",
                "--reuse-window");

        Assert.True(
            AppJumpListOpenRequestPolicy
                .TryBuild(
                    item,
                    application,
                    out ProcessStartInfo?
                        request));
        Assert.NotNull(request);
        Assert.Equal(
            @"C:\Apps\Editor.exe",
            request!.FileName);
        Assert.Equal(
            "--reuse-window "
            + "\"C:\\Docs\\计划.txt\"",
            request.Arguments);
    }

    [Fact]
    public void OpenRequest_ShellLinkKeepsExactTargetAndArguments()
    {
        var item =
            new AppJumpListItem(
                "工作区",
                @"C:\Apps\Editor.exe",
                "--workspace \"C:\\Code\"")
            {
                Source =
                    AppJumpListItemSource
                        .ShellLink
            };
        var application =
            new AppJumpListApplicationLaunch(
                AppLaunchKind.Executable,
                @"D:\Other.exe",
                null);

        Assert.True(
            AppJumpListOpenRequestPolicy
                .TryBuild(
                    item,
                    application,
                    out ProcessStartInfo?
                        request));
        Assert.Equal(
            @"C:\Apps\Editor.exe",
            request!.FileName);
        Assert.Equal(
            "--workspace \"C:\\Code\"",
            request.Arguments);
    }

    [Fact]
    public void OpenRequest_PackagedAppFallsBackToDocumentAssociation()
    {
        var item =
            new AppJumpListItem(
                "照片",
                @"C:\Photos\a.jpg",
                null);
        var application =
            new AppJumpListApplicationLaunch(
                AppLaunchKind.ShellApp,
                "Microsoft.Photos_8wekyb3d8bbwe!App",
                null);

        Assert.True(
            AppJumpListOpenRequestPolicy
                .TryBuild(
                    item,
                    application,
                    out ProcessStartInfo?
                        request));
        Assert.Equal(
            @"C:\Photos\a.jpg",
            request!.FileName);
        Assert.Equal(
            string.Empty,
            request.Arguments);
    }

    private sealed class
        FakeJumpListNative :
            IAppJumpListNative
    {
        public bool ThrowOnRead
        {
            get;
            init;
        }

        public AppJumpListCategory?
            ThrowCategory
        {
            get;
            init;
        }

        public int ReadCount
        {
            get;
            private set;
        }

        public List<AppJumpListCategory>
            ObservedCategories
        {
            get;
        } = new();

        public string? ObservedAppId
        {
            get;
            private set;
        }

        public ApartmentState
            ObservedApartment
        {
            get;
            private set;
        }

        public int ObservedThreadId
        {
            get;
            private set;
        }

        public IReadOnlyList<
            AppJumpListItem> Read(
                string
                    applicationUserModelId,
                AppJumpListCategory category,
                int limit)
        {
            ReadCount++;
            ObservedCategories.Add(
                category);
            ObservedAppId =
                applicationUserModelId;
            ObservedApartment =
                Thread.CurrentThread
                    .GetApartmentState();
            ObservedThreadId =
                Environment
                    .CurrentManagedThreadId;
            if (ThrowOnRead
                || ThrowCategory
                    == category)
            {
                throw new
                    InvalidOperationException(
                        "shell busy");
            }

            return new[]
            {
                new AppJumpListItem(
                    "文档",
                    @"C:\Docs\a.txt",
                    null)
            };
        }
    }

    private sealed class
        BlockingJumpListNative :
            IAppJumpListNative
    {
        private readonly
            ManualResetEventSlim _started;
        private readonly
            ManualResetEventSlim _release;

        internal BlockingJumpListNative(
            ManualResetEventSlim started,
            ManualResetEventSlim release)
        {
            _started = started;
            _release = release;
        }

        public IReadOnlyList<
            AppJumpListItem> Read(
                string
                    applicationUserModelId,
                AppJumpListCategory category,
                int limit)
        {
            _started.Set();
            _release.Wait(
                TimeSpan.FromSeconds(5));
            return new[]
            {
                new AppJumpListItem(
                    "迟到文档",
                    @"C:\Docs\late.txt",
                    null)
            };
        }
    }
}
