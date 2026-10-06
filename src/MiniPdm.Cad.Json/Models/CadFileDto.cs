using System.Text.Json.Serialization;

namespace MiniPdm.Cad.Json.Models;

/// <summary>DTO формы JSON-документа CAD-системы. Отражает формат обмена.
/// Маппинг в CadDocument - с нормализацией имен (ADR 0005).</summary>
internal sealed class CadFileDto
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("designation")]
    public string? Designation { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("properties")]
    public CadPropertiesDto? Properties { get; set; }

    [JsonPropertyName("components")]
    public List<CadComponentDto>? Components { get; set; }
}