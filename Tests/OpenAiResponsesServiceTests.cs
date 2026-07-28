using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OpenAiResponsesServiceTests
{
    [Fact]
    public async Task CompleteAsync_SendsResponsesRequestAndAggregatesText()
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
                    """
                    {
                      "output": [
                        { "content": [
                          { "type": "output_text", "text": "第一段" },
                          { "type": "refusal", "refusal": "ignored" }
                        ] },
                        { "content": [
                          { "type": "output_text", "text": "第二段" }
                        ] }
                      ]
                    }
                    """);
            });
        using var client = new HttpClient(handler);
        using var service =
            new OpenAiResponsesService(client);

        string result = await service.CompleteAsync(
            "secret-key",
            "gpt-5.6-sol",
            "系统指令",
            "用户输入",
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(
            OpenAiResponsesService.ResponsesEndpoint,
            captured!.RequestUri!.ToString());
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal(
            "secret-key",
            captured.Headers.Authorization.Parameter);
        using JsonDocument requestJson = JsonDocument.Parse(body!);
        Assert.Equal(
            "gpt-5.6-sol",
            requestJson.RootElement
                .GetProperty("model")
                .GetString());
        Assert.Equal(
            "none",
            requestJson.RootElement
                .GetProperty("reasoning")
                .GetProperty("effort")
                .GetString());
        Assert.Equal("第一段" + Environment.NewLine
            + Environment.NewLine + "第二段", result);
    }

    [Fact]
    public async Task CompleteAsync_MapsUnauthorizedErrorWithoutLeakingKey()
    {
        var handler = new StubHandler(
            (_, _) => Task.FromResult(
                JsonResponse(
                    """{"error":{"message":"invalid credential"}}""",
                    HttpStatusCode.Unauthorized)));
        using var service =
            new OpenAiResponsesService(new HttpClient(handler));

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAsync(
                    "never-print-this-key",
                    "gpt-5.6-sol",
                    "instruction",
                    "input",
                    CancellationToken.None));

        Assert.Contains("API Key 无效", error.Message);
        Assert.DoesNotContain(
            "never-print-this-key",
            error.Message);
    }

    [Fact]
    public async Task CompleteAsync_RejectsEmptyOutput()
    {
        var handler = new StubHandler(
            (_, _) => Task.FromResult(
                JsonResponse("""{"output":[]}""")));
        using var service =
            new OpenAiResponsesService(new HttpClient(handler));

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAsync(
                    "key",
                    "gpt-5.6-sol",
                    "instruction",
                    "input",
                    CancellationToken.None));

        Assert.Contains("空响应", error.Message);
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
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _handler;

        internal StubHandler(
            Func<
                HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
