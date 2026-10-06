namespace MiniPdm.App.Models;

/// <summary>
/// Модель строки состава 1-го уровня
/// </summary>
/// <param name="Designation">Обозначение</param>
/// <param name="Name">Наименование</param>
/// <param name="Quantity">Количество</param>
/// <param name="UnitMass">Масса 1 шт.</param>
public sealed record CompositionRow(string Designation, string Name, int Quantity, string UnitMass);