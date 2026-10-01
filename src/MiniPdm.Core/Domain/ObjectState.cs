namespace MiniPdm.Core.Domain;

/// <summary>Состояние версии объекта.</summary>
/// <remarks> Допустимые переходы — в <see cref="StateRules"/>.
/// См. решение adr в 0001-reference-values-text-check</remarks>
public enum ObjectState
{
    /// <summary>В работе: версию можно изменять.</summary>
    InWork,

    /// <summary>Утверждено: изменять нельзя, изменения — только через новую версию.</summary>
    Approved,

    /// <summary>Аннулировано: не участвует в расчётах.</summary>
    Annulled
}