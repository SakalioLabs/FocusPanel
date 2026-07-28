using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellFolderPickerServiceTests
{
    [Fact]
    public void PickFolder_ForwardsRequestAndOwner()
    {
        FolderPickerRequest? observedRequest =
            null;
        IntPtr observedOwner = IntPtr.Zero;
        var boundary = new RecordingBoundary(
            (request, owner) =>
            {
                observedRequest = request;
                observedOwner = owner;
                return FolderPickerResult.Selected(
                    @"D:\Tasks\Images");
            });
        var service = new ShellFolderPickerService(
            boundary,
            () => new IntPtr(42));
        var request = new FolderPickerRequest(
            "选择任务图片保存位置",
            @"D:\Tasks",
            "使用此文件夹");

        FolderPickerResult result =
            service.PickFolder(request);

        Assert.Same(request, observedRequest);
        Assert.Equal(new IntPtr(42), observedOwner);
        Assert.Equal(
            FolderPickerStatus.Selected,
            result.Status);
        Assert.Equal(
            @"D:\Tasks\Images",
            result.Path);
    }

    [Fact]
    public void PickFolder_PreservesCancellation()
    {
        var service = new ShellFolderPickerService(
            new RecordingBoundary(
                (_, _) =>
                    FolderPickerResult.Canceled()),
            () => IntPtr.Zero);

        FolderPickerResult result =
            service.PickFolder(
                new FolderPickerRequest(
                    "选择文件夹"));

        Assert.Equal(
            FolderPickerStatus.Canceled,
            result.Status);
        Assert.Null(result.Path);
        Assert.Null(result.Error);
    }

    [Fact]
    public void PickFolder_HoldsPanelInteractionForDialogLifetime()
    {
        var host = new RecordingInteractionHost();
        var boundary = new RecordingBoundary(
            (_, _) =>
            {
                Assert.Equal(1, host.BeginCount);
                Assert.Equal(0, host.EndCount);
                return FolderPickerResult.Canceled();
            });
        var service = new ShellFolderPickerService(
            boundary,
            () => IntPtr.Zero,
            () => host);

        _ = service.PickFolder(
            new FolderPickerRequest(
                "选择文件夹"));

        Assert.Equal(1, host.BeginCount);
        Assert.Equal(1, host.EndCount);
    }

    [Fact]
    public void PickFolder_ConvertsBoundaryExceptionToFailure()
    {
        var service = new ShellFolderPickerService(
            new RecordingBoundary(
                (_, _) =>
                    throw new InvalidOperationException(
                        "COM unavailable")),
            () => IntPtr.Zero);

        FolderPickerResult result =
            service.PickFolder(
                new FolderPickerRequest(
                    "选择文件夹"));

        Assert.Equal(
            FolderPickerStatus.Failed,
            result.Status);
        Assert.Contains(
            "COM unavailable",
            result.Error);
    }

    [Fact]
    public void PickFolder_RejectsMissingTitleWithoutOpeningBoundary()
    {
        int calls = 0;
        var service = new ShellFolderPickerService(
            new RecordingBoundary(
                (_, _) =>
                {
                    calls++;
                    return FolderPickerResult.Canceled();
                }),
            () => IntPtr.Zero);

        FolderPickerResult result =
            service.PickFolder(
                new FolderPickerRequest(" "));

        Assert.Equal(0, calls);
        Assert.Equal(
            FolderPickerStatus.Failed,
            result.Status);
        Assert.Contains(
            "缺少标题",
            result.Error);
    }

    [Fact]
    public void SelectionPolicy_CancelDoesNotApplyOrReportError()
    {
        FolderSelectionDecision decision =
            FolderSelectionPolicy.Resolve(
                FolderPickerResult.Canceled());

        Assert.False(decision.ShouldApply);
        Assert.Null(decision.Path);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void SelectionPolicy_SelectedPathCanBeApplied()
    {
        FolderSelectionDecision decision =
            FolderSelectionPolicy.Resolve(
                FolderPickerResult.Selected(
                    @"D:\Tasks\Images"));

        Assert.True(decision.ShouldApply);
        Assert.Equal(
            @"D:\Tasks\Images",
            decision.Path);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void SelectionPolicy_FailureIsReportedWithoutApplying()
    {
        FolderSelectionDecision decision =
            FolderSelectionPolicy.Resolve(
                FolderPickerResult.Failed(
                    "访问被拒绝"));

        Assert.False(decision.ShouldApply);
        Assert.Null(decision.Path);
        Assert.Equal(
            "访问被拒绝",
            decision.Error);
    }

    private sealed class RecordingBoundary
        : IShellFolderDialogBoundary
    {
        private readonly Func<
            FolderPickerRequest,
            IntPtr,
            FolderPickerResult> _show;

        internal RecordingBoundary(
            Func<
                FolderPickerRequest,
                IntPtr,
                FolderPickerResult> show)
        {
            _show = show;
        }

        public FolderPickerResult Show(
            FolderPickerRequest request,
            IntPtr ownerHandle) =>
            _show(
                request,
                ownerHandle);
    }

    private sealed class RecordingInteractionHost
        : IFocusDialogInteractionHost
    {
        internal int BeginCount { get; private set; }
        internal int EndCount { get; private set; }

        public void BeginTransientInteraction() =>
            BeginCount++;

        public void EndTransientInteraction() =>
            EndCount++;
    }
}
