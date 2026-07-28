using System;
using System.Windows;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowsFilePickerServiceTests
{
    [Fact]
    public void PickFile_ForwardsRequestAndResult()
    {
        FilePickerRequest? observed = null;
        var boundary = new RecordingBoundary(
            (request, owner) =>
            {
                observed = request;
                Assert.Null(owner);
                return FilePickerResult.Selected(
                    @"D:\Pictures\demo.png");
            });
        var service = new WindowsFilePickerService(
            boundary,
            () => null);
        var request = new FilePickerRequest(
            "选择要插入的图片",
            "图片文件|*.png",
            @"D:\Pictures");

        FilePickerResult result =
            service.PickFile(request);

        Assert.Same(request, observed);
        Assert.Equal(
            FilePickerStatus.Selected,
            result.Status);
        Assert.Equal(
            @"D:\Pictures\demo.png",
            result.Path);
    }

    [Fact]
    public void PickFile_HoldsPanelInteractionForDialogLifetime()
    {
        var host = new RecordingInteractionHost();
        var service = new WindowsFilePickerService(
            new RecordingBoundary(
                (_, _) =>
                {
                    Assert.Equal(
                        1,
                        host.BeginCount);
                    Assert.Equal(
                        0,
                        host.EndCount);
                    return FilePickerResult.Canceled();
                }),
            () => null,
            () => host);

        _ = service.PickFile(
            new FilePickerRequest(
                "选择图片",
                "图片|*.png"));

        Assert.Equal(1, host.BeginCount);
        Assert.Equal(1, host.EndCount);
    }

    [Fact]
    public void PickFile_ConvertsBoundaryExceptionToFailure()
    {
        var service = new WindowsFilePickerService(
            new RecordingBoundary(
                (_, _) =>
                    throw new InvalidOperationException(
                        "dialog unavailable")),
            () => null);

        FilePickerResult result =
            service.PickFile(
                new FilePickerRequest(
                    "选择图片",
                    "图片|*.png"));

        Assert.Equal(
            FilePickerStatus.Failed,
            result.Status);
        Assert.Contains(
            "dialog unavailable",
            result.Error);
    }

    [Theory]
    [InlineData("", "图片|*.png", "缺少标题")]
    [InlineData("选择图片", "", "缺少文件类型")]
    public void PickFile_RejectsInvalidRequestWithoutOpeningBoundary(
        string title,
        string filter,
        string expectedError)
    {
        int calls = 0;
        var service = new WindowsFilePickerService(
            new RecordingBoundary(
                (_, _) =>
                {
                    calls++;
                    return FilePickerResult.Canceled();
                }),
            () => null);

        FilePickerResult result =
            service.PickFile(
                new FilePickerRequest(
                    title,
                    filter));

        Assert.Equal(0, calls);
        Assert.Equal(
            FilePickerStatus.Failed,
            result.Status);
        Assert.Contains(
            expectedError,
            result.Error);
    }

    [Fact]
    public void SelectionPolicy_CancelDoesNotOpenOrReportError()
    {
        FileSelectionDecision decision =
            FileSelectionPolicy.Resolve(
                FilePickerResult.Canceled());

        Assert.False(decision.ShouldOpen);
        Assert.Null(decision.Path);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void SelectionPolicy_SelectedFileCanBeOpened()
    {
        FileSelectionDecision decision =
            FileSelectionPolicy.Resolve(
                FilePickerResult.Selected(
                    @"D:\Pictures\demo.png"));

        Assert.True(decision.ShouldOpen);
        Assert.Equal(
            @"D:\Pictures\demo.png",
            decision.Path);
    }

    [Fact]
    public void SelectionPolicy_FailureDoesNotOpen()
    {
        FileSelectionDecision decision =
            FileSelectionPolicy.Resolve(
                FilePickerResult.Failed(
                    "访问被拒绝"));

        Assert.False(decision.ShouldOpen);
        Assert.Equal(
            "访问被拒绝",
            decision.Error);
    }

    private sealed class RecordingBoundary
        : IFileDialogBoundary
    {
        private readonly Func<
            FilePickerRequest,
            Window?,
            FilePickerResult> _show;

        internal RecordingBoundary(
            Func<
                FilePickerRequest,
                Window?,
                FilePickerResult> show)
        {
            _show = show;
        }

        public FilePickerResult Show(
            FilePickerRequest request,
            Window? owner) =>
            _show(request, owner);
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
