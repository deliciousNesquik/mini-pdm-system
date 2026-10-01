namespace MiniPdm.Core.Cad;

/// <summary>
/// Источник документов CAD-системы. Сейчас реализация - JSON-файлы
/// </summary>
public interface ICadDocumentReader
{
    /// <summary>
    /// Читает документ по его пути.
    /// </summary>
    /// <exception cref="CadDocumentException">Документ не читается:
    /// повреждённые данные, неподдерживаемая версия формата.</exception>
    Task<CadDocument> ReadAsync(string path, CancellationToken ct);

    /// <summary>
    /// Перечисляет полные пути документов в папке, пригодные для передачи в <see cref="ReadAsync"/>
    /// без преобразований. Расширение контракта относительно ТЗ (ADR 0006): перечисление файлов -
    /// тоже работа с файловой системой, и оно не должно просачиваться в сервис импорта.
    /// </summary>
    Task<IReadOnlyList<string>> ListDocumentsAsync(string folder, CancellationToken ct);
}