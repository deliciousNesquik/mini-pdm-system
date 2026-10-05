using MiniPdm.Core.Domain;

namespace MiniPdm.Data;

public sealed record PdmObjectListItem(
    long Id, ObjectType Type, string? Designation, string Name,
    int? CurrentVersionNo, ObjectState? CurrentState);