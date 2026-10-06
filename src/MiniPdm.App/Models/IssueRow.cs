using MiniPdm.Core.Import;

namespace MiniPdm.App.Models;

/// <summary>
/// Строка отчёта импорта. Содержит имя файла, результат анализа, причину и уровень серьёзности (ошибка или предупреждение).
/// </summary>
/// <param name="FileName">Имя файла</param>
/// <param name="Result">Результат анализа</param>
/// <param name="Reason">Причина</param>
/// <param name="Severity">Уровень серьёзности</param>
public sealed record IssueRow(string FileName, string Result, string Reason, ImportSeverity Severity);