using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class UpdateFailureMessageTests
{
    [Fact]
    public void Forbidden_ExplainsRateLimitAndManualFallback()
    {
        string message = UpdateFailureMessage.Describe(
            new HttpRequestException(
                "Response status code does not indicate success: 403.",
                null,
                HttpStatusCode.Forbidden));

        Assert.Contains("限制", message);
        Assert.Contains("官方下载页", message);
        Assert.DoesNotContain(
            "Response status code",
            message);
    }

    [Fact]
    public void WrappedRateLimit_IsRecognized()
    {
        string message = UpdateFailureMessage.Describe(
            new InvalidOperationException(
                "outer",
                new Exception("API rate limit exceeded")));

        Assert.Contains("GitHub", message);
        Assert.Contains("手动安装", message);
    }

    [Fact]
    public void Timeout_GivesNetworkRecovery()
    {
        string message = UpdateFailureMessage.Describe(
            new TaskCanceledException());

        Assert.Contains("超时", message);
        Assert.Contains("检查网络", message);
    }

    [Fact]
    public void AccessDenied_GivesInstallerRecovery()
    {
        string message = UpdateFailureMessage.Describe(
            new UnauthorizedAccessException());

        Assert.Contains("写入权限", message);
        Assert.Contains("安装程序", message);
    }

    [Fact]
    public void FileFailure_DoesNotExposeRawException()
    {
        string message = UpdateFailureMessage.Describe(
            new IOException("raw operating system message"));

        Assert.Contains("占用", message);
        Assert.DoesNotContain("raw operating", message);
    }
}
