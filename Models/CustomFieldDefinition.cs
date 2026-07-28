using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FocusPanel.Models;

public enum CustomFieldType
{
    ShortText,
    MultiSelect,
    SingleSelect,
    LongText // Markdown
}

public class CustomFieldDefinition
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public CustomFieldType Type { get; set; }

    [JsonIgnore]
    public string TypeDisplay => Type switch
    {
        CustomFieldType.ShortText => "短文本",
        CustomFieldType.LongText => "长文本 / Markdown",
        CustomFieldType.SingleSelect => "单选",
        CustomFieldType.MultiSelect => "多选",
        _ => Type.ToString()
    };
    
    // For Select types: comma-separated or JSON string of options
    public List<string> Options { get; set; } = new List<string>();
}
