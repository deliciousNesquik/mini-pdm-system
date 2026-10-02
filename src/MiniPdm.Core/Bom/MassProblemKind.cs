namespace MiniPdm.Core.Bom;

public enum MassProblemKind
{
    /// <summary>У детали/стандартного изделия не указана масса (ТЗ: назвать виновника, а не вернуть неполную сумму).</summary>
    MissingMass,

    /// <summary>У объекта нет действующей версии — его состав не раскрыт, сумма неполна (ADR 0009).</summary>
    NoActiveVersion
}