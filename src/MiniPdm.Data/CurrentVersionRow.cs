using MiniPdm.Core.Domain;

namespace MiniPdm.Data;

public sealed record CurrentVersionRow(
    long ObjectId, long? VersionId, ObjectState? State, string? Material, decimal? MassKg);