using System.Text.Json.Serialization;

namespace MiniPdm.Cad.Json;

internal sealed class CadPropertiesDto
{
    [JsonPropertyName("material")]
    public string? Material { get; set; }

    [JsonPropertyName("mass")]
    public decimal? Mass { get; set; }
}