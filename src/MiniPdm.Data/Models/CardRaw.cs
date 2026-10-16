using MiniPdm.Core.Domain;

namespace MiniPdm.Data.Models;

public sealed class CardRaw
{
    public long Id { get; set; }
    public ObjectType Type { get; set; }
    public string? Designation { get; set; }
    public string Name { get; set; } = "";
    public long? CurrentVersionId { get; set; }
    public int? CurrentVersionNo { get; set; }
    public ObjectState? CurrentState { get; set; }
    public string? Material { get; set; }
    public decimal? MassKg { get; set; }
}