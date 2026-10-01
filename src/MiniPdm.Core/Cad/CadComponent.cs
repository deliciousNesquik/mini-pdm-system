namespace MiniPdm.Core.Cad;

/// <summary>
/// Компонент сборки: ссылка на документ-потомок и количество.
/// Имя потомка — идентификатор документа, нормализованный адаптером (ADR 0005).
/// </summary>
public readonly record struct CadComponent(string FileName, int Count);