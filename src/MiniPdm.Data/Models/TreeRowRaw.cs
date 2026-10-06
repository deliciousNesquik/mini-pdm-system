namespace MiniPdm.Data.Models;

public sealed class TreeRowRaw
{
    public long[] Path { get; set; } = [];
    public string Type { get; set; } = "";
    public string? Designation { get; set; }
    public string Name { get; set; } = "";
    public string? State { get; set; }
    public int QuantityOnPath { get; set; }
    public bool HasActiveVersion { get; set; }
    public decimal? UnitMassKg { get; set; }
}