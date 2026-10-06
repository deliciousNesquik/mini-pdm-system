using MiniPdm.Core.Domain;

namespace MiniPdm.Data.Models;

public sealed record VersionRow(long ObjectId, int VersionNo, ObjectState State, long? CurrentVersionId);