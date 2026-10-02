using System.Text.Json.Serialization;

namespace MiniPdm.Cad.Json;

internal sealed class CadComponentDto
{
    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}