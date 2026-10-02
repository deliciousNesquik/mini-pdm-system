using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Import;

/// <summary>Компонент состава снимка — по доменной идентичности, не по id.</summary>
public sealed record ExistingChild(ObjectType Type, string? Designation, string Name, int Quantity);