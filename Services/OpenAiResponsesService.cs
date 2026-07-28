using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public sealed class OpenAiResponsesService : IAiAssistantService, IDisposable
{
    internal const string ResponsesEndpoint =
        "https://api.openai.com/v1/responses";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public OpenAiResponsesService()
        : this(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        }, true)
    {
    }

    internal OpenAiResponsesService(
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
            throw new InvalidOperationException("请先保存 OpenAI API Key。");

        var payload = new
        {
            model,
            reasoning = new { effort = "none" },
            instructions,
            input
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            ResponsesEndpoint);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response =
            await _httpClient.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw CreateApiException(response.StatusCode, json);

        using JsonDocument document = JsonDocument.Parse(json);
        var parts = new List<string>();
        if (document.RootElement.TryGetProperty(
                "output",
                out JsonElement output)
            && output.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in output.EnumerateArray())
            {
                if (!item.TryGetProperty(
                        "content",
                        out JsonElement content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement block in content.EnumerateArray())
                {
                    if (block.TryGetProperty(
                            "type",
                            out JsonElement type)
                        && type.GetString() == "output_text"
                        && block.TryGetProperty(
                            "text",
                            out JsonElement text))
                    {
                        string? value = text.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            parts.Add(value.Trim());
                    }
                }
            }
        }

        string result = string.Join(
            Environment.NewLine + Environment.NewLine,
            parts);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("AI 返回了空响应，请稍后重试。");
        return result;
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
                "API Key 无效或已失效",
            HttpStatusCode.TooManyRequests =>
                "请求过于频繁或账户额度不足",
            _ => $"OpenAI 请求失败（{(int)statusCode}）"
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
