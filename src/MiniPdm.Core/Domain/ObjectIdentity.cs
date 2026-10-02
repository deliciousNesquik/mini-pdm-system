namespace MiniPdm.Core.Domain;

/// <summary>
/// Доменная идентичность объекта: обозначение - уникально среди сборок и деталей,
/// наименование среди стандартных изделий.
/// </summary>
public static class ObjectIdentity
{
    /// <summary>Стабильный строковый ключ идентичности. Префикс разводит
    /// "обозначение" и "наименование", чтобы ключи не пересекались.</summary>
    public static string Key(ObjectType type, string? designation, string name) => type switch
    {
        ObjectType.Assembly or ObjectType.Part => "D:" + designation,
        _ => "N:" + name
    };
}