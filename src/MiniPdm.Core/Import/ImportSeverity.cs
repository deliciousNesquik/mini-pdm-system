namespace MiniPdm.Core.Import;

/// <summary>Тяжесть вердикта импорта. Принятые файлы в Issues не попадают —
/// они перечислены в ImportAnalysis.Importable (ТЗ: отчёт = ошибки + предупреждения + счётчики).</summary>
public enum ImportSeverity
{
    Error,
    Warning
}