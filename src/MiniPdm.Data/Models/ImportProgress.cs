namespace MiniPdm.Data.Models;

/// <summary>
/// Состояние процесса импорта
/// </summary>
/// <param name="Phase">Фаза процесса импорта</param>
/// <param name="Current">Текущее значение</param>
/// <param name="Total">Общее значение</param>
/// <param name="CurrentFile">Текущий обрабатываемый файл</param>
public sealed record ImportProgress(ImportPhase Phase, int Current, int? Total, string? CurrentFile);