using MiniPdm.Core.Domain;

namespace MiniPdm.Data.Models;

/// <summary>
/// Представляет элемент списка объектов PDM.
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Type">Тип</param>
/// <param name="Designation">Обозначение</param>
/// <param name="Name">Наименование</param>
/// <param name="CurrentVersionNo">Номер текущей версии</param>
/// <param name="CurrentState">Текущее состояние</param>
public sealed record PdmObjectListItem(
    long Id, ObjectType Type, string? Designation, string Name,
    int? CurrentVersionNo, ObjectState? CurrentState);