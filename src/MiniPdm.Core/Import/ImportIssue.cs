namespace MiniPdm.Core.Import;

// <summary>Один вердикт по одному файлу с человекочитаемой причиной.</summary>
public sealed record ImportIssue(string FileName, ImportSeverity Severity, string Reason);