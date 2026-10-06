namespace MiniPdm.Data.Models;

public sealed record AppStatus(
    string DatabaseKind,
    string ConnectionSummary,
    int ObjectsCount,
    DateTime? LastImportAt);