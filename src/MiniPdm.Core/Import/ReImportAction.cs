namespace MiniPdm.Core.Import;

/// <summary>Решение по одному документу при повторном импорте.</summary>
public enum ReImportAction
{
    /// <summary>Объекта нет в БД — создать, версия 1 «В работе».</summary>
    CreateObject,

    /// <summary>Данные не изменились — ничего не происходит.</summary>
    Unchanged,

    /// <summary>Текущая версия «В работе» — обновить её.</summary>
    UpdateInWorkVersion,

    /// <summary>Текущая версия «Утверждено» (или действующей нет) — новая версия «В работе».</summary>
    CreateNewVersion,

    /// <summary>В БД объект с таким обозначением/наименованием другого типа — ошибка импорта (ADR: тип менять нельзя).</summary>
    TypeConflict
}