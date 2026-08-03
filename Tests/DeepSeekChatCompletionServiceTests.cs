using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DeepSeekChatCompletionServiceTests
{
    [Fact]
    public async Task CompleteAsync_UsesOfficialChatEndpointAndParsesContent()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new StubHandler(
            async (request, cancellationToken) =>
            {
                captured = request;
                body = await request.Content!.ReadAsStringAsync(
                    cancellationToken);
                return JsonResponse(
                    """{"choices":[{"message":{"content":"分类完成"}}]}""");
            });
        using var service = new DeepSeekChatCompletionService(
            new HttpClient(handler));

        string result = await service.CompleteAsync(
            "ds-key",
            "deepseek-v4-flash",
            "system",
            "input",
            CancellationToken.None);

        Assert.Equal("分类完成", result);
        Assert.Equal(
            DeepSeekChatCompletionService.ChatCompletionEndpoint,
            captured!.RequestUri!.ToString());
        Assert.Equal(
            "ds-key",
            captured.Headers.Authorization!.Parameter);
        using JsonDocument document = JsonDocument.Parse(body!);
        Assert.Equal(
            "deepseek-v4-flash",
            document.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            "disabled",
            document.RootElement
                .GetProperty("thinking")
                .GetProperty("type")
                .GetString());
    }

    [Fact]
    public async Task CompleteAsync_DoesNotLeakRejectedKey()
    {
        var handler = new StubHandler(
            (_, _) => Task.FromResult(
                JsonResponse(
                    """{"error":{"message":"invalid"}}""",
                    HttpStatusCode.Unauthorized)));
        using var service = new DeepSeekChatCompletionService(
            new HttpClient(handler));

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAsync(
                    "secret-never-log",
                    "deepseek-v4-flash",
                    "system",
                    "input",
                    CancellationToken.None));

        Assert.Contains("API Key 无效", error.Message);
        Assert.DoesNotContain("secret-never-log", error.Message);
    }

    private static HttpResponseMessage JsonResponse(
        string body,
        HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(
                body,
                Encoding.UTF8,
                "application/json")
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> _handler;

        internal StubHandler(
            Func<HttpRequestMessage, CancellationToken,
                Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
