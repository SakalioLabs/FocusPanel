using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;

namespace FocusPanel.Services;

public class FeishuAuthService
{
    private const string AppIdKey = "feishu_app_id";
    private const string AppSecretKey = "feishu_app_secret";
    private const string TokenUrl = "https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal";

    private static readonly HttpClient _httpClient = new();
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly object _lock = new();

    public bool IsConfigured
    {
        get
        {
            try
            {
                using var context = new AppDbContext();
                context.EnsureSchema();
                var appId = context.AppConfigs.Find(AppIdKey);
                var appSecret = context.AppConfigs.Find(AppSecretKey);
                return appId != null && !string.IsNullOrEmpty(appId.Value)
                    && appSecret != null && !string.IsNullOrEmpty(appSecret.Value);
            }
            catch { return false; }
        }
    }

    public string? GetAppId()
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        return context.AppConfigs.Find(AppIdKey)?.Value;
    }

    public string? GetAppSecret()
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        return context.AppConfigs.Find(AppSecretKey)?.Value;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        lock (_lock)
        {
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
                return _cachedToken;
        }

        var appId = GetAppId();
        var appSecret = GetAppSecret();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
            throw new InvalidOperationException("Feishu app credentials not configured.");

        var payload = new { app_id = appId, app_secret = appSecret };
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(TokenUrl, content);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<FeishuTokenResponse>(json);

        if (result == null || result.Code != 0)
            throw new FeishuApiException(result?.Code ?? -1, result?.Msg ?? "Token request failed");

        lock (_lock)
        {
            _cachedToken = result.TenantAccessToken!;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(result.Expire);
            return _cachedToken;
        }
    }

    public void InvalidateToken()
    {
        lock (_lock)
        {
            _cachedToken = null;
            _tokenExpiry = DateTime.MinValue;
        }
    }

    public async Task<bool> ValidateCredentialsAsync(string appId, string appSecret)
    {
        try
        {
            var payload = new { app_id = appId, app_secret = appSecret };
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(TokenUrl, content);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FeishuTokenResponse>(json);
            return result?.Code == 0;
        }
        catch
        {
            return false;
        }
    }

    public void SaveCredentials(string appId, string appSecret)
    {
        using var context = new AppDbContext();
        context.EnsureSchema();

        foreach (var (key, value) in new[] { (AppIdKey, appId), (AppSecretKey, appSecret) })
        {
            var config = context.AppConfigs.Find(key);
            if (config == null)
            {
                context.AppConfigs.Add(new AppConfig { Key = key, Value = value });
            }
            else
            {
                config.Value = value;
            }
        }
        context.SaveChanges();
        InvalidateToken();
    }

    public void ClearCredentials()
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        foreach (var key in new[] { AppIdKey, AppSecretKey })
        {
            var config = context.AppConfigs.Find(key);
            if (config != null)
            {
                context.AppConfigs.Remove(config);
            }
        }
        context.SaveChanges();
        InvalidateToken();
    }
}
