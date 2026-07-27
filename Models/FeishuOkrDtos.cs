using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FocusPanel.Models;

public class FeishuObjectiveDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("key_results")]
    public List<FeishuKrDto> KeyResults { get; set; } = new();
}

public class FeishuKrDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("current_value")]
    public double CurrentValue { get; set; }

    [JsonPropertyName("start_value")]
    public double StartValue { get; set; }

    [JsonPropertyName("target_value")]
    public double TargetValue { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "%";

    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }
}

public class CreateObjectiveRequest
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    [JsonPropertyName("key_results")]
    public List<CreateKrRequest> KeyResults { get; set; } = new();
}

public class UpdateObjectiveRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("weight")]
    public double? Weight { get; set; }
}

public class CreateKrRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("start_value")]
    public double StartValue { get; set; }

    [JsonPropertyName("target_value")]
    public double TargetValue { get; set; } = 100;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "%";

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;
}

public class UpdateKrRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("current_value")]
    public double? CurrentValue { get; set; }

    [JsonPropertyName("start_value")]
    public double? StartValue { get; set; }

    [JsonPropertyName("target_value")]
    public double? TargetValue { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("weight")]
    public double? Weight { get; set; }
}

public class ProgressRecordRequest
{
    [JsonPropertyName("current_value")]
    public double CurrentValue { get; set; }
}

public class FeishuUserMeResponse
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class FeishuEmptyResponse
{
}

public class FeishuOkrListResponse<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("page_token")]
    public string? PageToken { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
