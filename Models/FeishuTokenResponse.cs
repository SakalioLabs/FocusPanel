using System.Text.Json.Serialization;

namespace FocusPanel.Models;

public class FeishuTokenResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("tenant_access_token")]
    public string? TenantAccessToken { get; set; }

    [JsonPropertyName("expire")]
    public int Expire { get; set; }
}

public class FeishuApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}
