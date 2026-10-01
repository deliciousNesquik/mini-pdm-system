namespace MiniPdm.Core.Cad;

/// <summary>
/// Файл не может быть прочитан как документ CAD-системы: повреждённые данные,
/// неподдерживаемая версия формата. В отчёте импорта означает вердикт «Ошибка»,
/// импорт остальных документов продолжается.
/// </summary>
public sealed class CadDocumentException : Exception
{
    public CadDocumentException(string message) : base(message) { }

    public CadDocumentException(string message, Exception innerException)
        : base(message, innerException) { }
}