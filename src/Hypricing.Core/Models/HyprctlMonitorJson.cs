using System.Text.Json.Serialization;

namespace Hypricing.Core.Models;

/// <summary>
/// AOT-safe DTO matching the JSON objects returned by <c>hyprctl monitors -j</c>.
/// Only the fields Hypricing needs are mapped; everything else is ignored.
/// </summary>
internal sealed class HyprctlMonitorJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("availableModes")]
    public List<string> AvailableModes { get; set; } = [];
}

[JsonSerializable(typeof(List<HyprctlMonitorJson>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
internal sealed partial class HyprctlMonitorJsonContext : JsonSerializerContext { }

