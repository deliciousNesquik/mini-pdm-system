using MiniPdm.Core.Domain;

namespace MiniPdm.Data;

public sealed record ObjectRow(long Id, ObjectType Type, string? Designation, string Name);