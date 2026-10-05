namespace MiniPdm.Data;

/// <summary>
/// Фазы импорта.
/// </summary>
public enum ImportPhase
{
    /// <summary>Перечисление и чтение документов.</summary>
    Reading,

    /// <summary>Анализ набора.</summary>
    Analyzing,

    /// <summary>Запись в БД внутри транзакции.</summary>
    Writing,

    /// <summary>Журнал и фиксация.</summary>
    Committing,

    /// <summary>Завершено (успешно или с отчётом об ошибках).</summary>
    Done
}