using MiniPdm.Core.Domain;

namespace MiniPdm.Data.Models;

public sealed record CompositionRow(
    long ParentObjectId, ObjectType ChildType, string? ChildDesignation, string ChildName, int Quantity);