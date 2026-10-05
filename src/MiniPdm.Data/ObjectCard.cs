using MiniPdm.Core.Domain;

namespace MiniPdm.Data;

/// <summary>
/// Представляет карточку объекта с его основными свойствами.
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Type">Тип</param>
/// <param name="Designation">Обозначение</param>
/// <param name="Name">Наименование</param>
/// <param name="CurrentVersionId">Идентификатор текущей версии</param>
/// <param name="CurrentVersionNo">Номер текущей версии</param>
/// <param name="CurrentState">Текущее состояние</param>
/// <param name="Material">Материал</param>
/// <param name="MassKg">Масса (кг)</param>
public sealed record ObjectCard(
    long Id,
    ObjectType Type,
    string? Designation,
    string Name,
    long? CurrentVersionId,
    int? CurrentVersionNo,
    ObjectState? CurrentState,
    string? Material,
    decimal? MassKg)
{
    public static ObjectCard Empty { get; } = new(0, ObjectType.Part, null, "", null, null, null, null, null);
}
    
    
