namespace MiniPdm.Data;

public sealed class ListItemRaw
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string? Designation { get; set; }
    public string Name { get; set; } = "";
    public int? CurrentVersionNo { get; set; }
    public string? CurrentState { get; set; }
}