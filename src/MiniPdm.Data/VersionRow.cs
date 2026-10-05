using MiniPdm.Core.Domain;

namespace MiniPdm.Data;

public sealed record VersionRow(long ObjectId, int VersionNo, ObjectState State, long? CurrentVersionId);