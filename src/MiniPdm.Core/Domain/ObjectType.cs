namespace MiniPdm.Core.Domain;

/// <summary>Тип объекта состава изделия</summary>
/// <remarks>Используется для определения типа объекта в составе изделия.
/// См. решение adr в 0001-reference-values-text-check</remarks>
public enum ObjectType
{
    /// <summary>Сборка</summary>
    Assembly,

    /// <summary>Деталь</summary>
    Part,

    /// <summary>Стандартное изделие</summary>
    StandardPart
}