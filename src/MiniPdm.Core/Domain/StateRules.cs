namespace MiniPdm.Core.Domain;

/// <summary>
/// Правила жизненного цикла версии объекта
/// </summary>
/// <remarks>смотреть раздел Предметная область ТЗ</remarks>
public static class StateRules
{
    /// <summary>
    /// Разрешен ли переход версии из состояния <paramref name="from"/> в <paramref name="to"/>.
    /// Разрешены: "В работе" -> "Утверждено", "В работе" -> "Аннулировано", "Утверждено" -> "Аннулировано".
    /// Обратных переходов нет.
    /// </summary>
    public static bool CanTransition(ObjectState from, ObjectState to) => (from, to) switch
    {
        (ObjectState.InWork, ObjectState.Approved)   => true,
        (ObjectState.InWork, ObjectState.Annulled)   => true,
        (ObjectState.Approved, ObjectState.Annulled) => true,
        _ => false
    };

    /// <summary>Версию можно изменять только в состоянии "В работе".</summary>
    public static bool IsEditable(ObjectState state) => state == ObjectState.InWork;

    /// <summary>Аннулированные версии не участвуют в расчетах.</summary>
    public static bool ParticipatesInCalculations(ObjectState state) => state != ObjectState.Annulled;
}