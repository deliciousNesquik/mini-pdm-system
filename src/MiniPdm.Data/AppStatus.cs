namespace MiniPdm.Data;

public sealed record AppStatus(
    string DatabaseKind,
    string ConnectionSummary,
    int ObjectsCount,
    DateTime? LastImportAt);