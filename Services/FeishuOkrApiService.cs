using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

public class FeishuOkrApiService
{
    private const string BaseUrl = "https://open.feishu.cn/open-apis/okr/v1";

    private static readonly HttpClient _httpClient = new();
    private readonly FeishuAuthService _authService;

    public FeishuOkrApiService(FeishuAuthService authService)
    {
        _authService = authService;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, object? body = null)
    {
        var token = await _authService.GetAccessTokenAsync();
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {token}");
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return request;
    }

    private async Task<T> SendWithRetryAsync<T>(Func<Task<HttpRequestMessage>> requestFactory) where T : class
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var request = await requestFactory();
            var response = await _httpClient.SendAsync(request);

            if ((int)response.StatusCode == 429)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? Math.Pow(2, attempt);
                await Task.Delay(TimeSpan.FromSeconds(retryAfter));
                continue;
            }

            var json = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _authService.InvalidateToken();
                if (attempt < maxRetries - 1) continue;
                throw new FeishuApiException(401, "Unauthorized");
            }

            var apiResponse = JsonSerializer.Deserialize<FeishuApiResponse<T>>(json);
            if (apiResponse == null)
                throw new FeishuApiException(-1, "Failed to parse response");

            if (apiResponse.Code != 0)
                throw new FeishuApiException(apiResponse.Code, apiResponse.Msg ?? "Unknown error");

            return apiResponse.Data!;
        }

        throw new FeishuApiException(-1, "Max retries exceeded");
    }

    public async Task<List<FeishuObjectiveDto>> GetObjectivesAsync(string userId)
    {
        var allObjectives = new List<FeishuObjectiveDto>();
        string? pageToken = null;

        do
        {
            var url = $"{BaseUrl}/users/{userId}/objectives?page_size=20";
            if (pageToken != null) url += $"&page_token={pageToken}";

            var result = await SendWithRetryAsync<FeishuOkrListResponse<FeishuObjectiveDto>>(async () =>
                await CreateRequestAsync(HttpMethod.Get, url));

            allObjectives.AddRange(result.Items);

            if (result.HasMore && !string.IsNullOrEmpty(result.PageToken))
                pageToken = result.PageToken;
            else
                pageToken = null;
        }
        while (pageToken != null);

        return allObjectives;
    }

    public async Task<FeishuObjectiveDto> CreateObjectiveAsync(CreateObjectiveRequest request)
    {
        var url = $"{BaseUrl}/objectives";
        return await SendWithRetryAsync<FeishuObjectiveDto>(async () =>
            await CreateRequestAsync(HttpMethod.Post, url, request));
    }

    public async Task<FeishuObjectiveDto> UpdateObjectiveAsync(string objectiveId, UpdateObjectiveRequest request)
    {
        var url = $"{BaseUrl}/objectives/{objectiveId}";
        return await SendWithRetryAsync<FeishuObjectiveDto>(async () =>
            await CreateRequestAsync(HttpMethod.Patch, url, request));
    }

    public async Task DeleteObjectiveAsync(string objectiveId)
    {
        var url = $"{BaseUrl}/objectives/{objectiveId}";
        await SendWithRetryAsync<FeishuEmptyResponse>(async () =>
            await CreateRequestAsync(HttpMethod.Delete, url));
    }

    public async Task<FeishuKrDto> UpdateKeyResultAsync(string krId, UpdateKrRequest request)
    {
        var url = $"{BaseUrl}/key_results/{krId}";
        return await SendWithRetryAsync<FeishuKrDto>(async () =>
            await CreateRequestAsync(HttpMethod.Patch, url, request));
    }

    public async Task RecordProgressAsync(string krId, double currentValue)
    {
        var url = $"{BaseUrl}/key_results/{krId}/progress_records";
        var body = new ProgressRecordRequest { CurrentValue = currentValue };
        await SendWithRetryAsync<FeishuEmptyResponse>(async () =>
            await CreateRequestAsync(HttpMethod.Post, url, body));
    }

    public async Task<string> GetCurrentUserIdAsync()
    {
        var url = "https://open.feishu.cn/open-apis/okr/v1/users/me";
        try
        {
            var result = await SendWithRetryAsync<FeishuUserMeResponse>(async () =>
                await CreateRequestAsync(HttpMethod.Get, url));
            return result.UserId ?? result.Id ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
