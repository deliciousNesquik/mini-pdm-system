namespace MiniPdm.Data;

public sealed class CardRaw
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string? Designation { get; set; }
    public string Name { get; set; } = "";
    public long? CurrentVersionId { get; set; }
    public int? CurrentVersionNo { get; set; }
    public string? CurrentState { get; set; }
    public string? Material { get; set; }
    public decimal? MassKg { get; set; }
}