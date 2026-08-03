using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public sealed class DeepSeekChatCompletionService :
    IAiAssistantService,
    IDisposable
{
    internal const string ChatCompletionEndpoint =
        "https://api.deepseek.com/chat/completions";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public DeepSeekChatCompletionService()
        : this(
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(90)
            },
            true)
    {
    }

    internal DeepSeekChatCompletionService(
        HttpClient httpClient,
        bool ownsClient = false)
    {
        _httpClient = httpClient;
        _ownsClient = ownsClient;
    }

    public async Task<string> CompleteAsync(
        string apiKey,
        string model,
        string instructions,
        string input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "请先保存 DeepSeek API Key。");

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = instructions },
                new { role = "user", content = input }
            },
            thinking = new { type = "disabled" },
            stream = false
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            ChatCompletionEndpoint);
        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                apiKey.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);
        string json = await response.Content.ReadAsStringAsync(
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw CreateApiException(response.StatusCode, json);

        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(
                "choices",
                out JsonElement choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty(
                "message",
                out JsonElement message)
            && message.TryGetProperty(
                "content",
                out JsonElement content))
        {
            string? result = content.GetString();
            if (!string.IsNullOrWhiteSpace(result))
                return result.Trim();
        }

        throw new InvalidOperationException(
            "DeepSeek 返回了空响应，请稍后重试。");
    }

    private static Exception CreateApiException(
        HttpStatusCode statusCode,
        string json)
    {
        string? message = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(
                    "error",
                    out JsonElement error)
                && error.TryGetProperty(
                    "message",
                    out JsonElement value))
            {
                message = value.GetString();
            }
        }
        catch (JsonException)
        {
        }

        string prefix = statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "DeepSeek API Key 无效或已失效",
            HttpStatusCode.TooManyRequests =>
                "DeepSeek 请求过于频繁或账户额度不足",
            _ => $"DeepSeek 请求失败（{(int)statusCode}）"
        };
        return new InvalidOperationException(
            string.IsNullOrWhiteSpace(message)
                ? prefix
                : $"{prefix}：{message}");
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }
}
