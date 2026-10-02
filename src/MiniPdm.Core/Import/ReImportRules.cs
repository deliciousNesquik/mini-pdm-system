using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Import;

/// <summary>Правила повторного импорта (ТЗ, «Предметная область»).</summary>
public static class ReImportRules
{
    /// <param name="incoming">Документ, прошедший <see cref="ImportAnalyzer"/> (все ссылки резолвятся).</param>
    /// <param name="existing">Снимок объекта в БД или null, если объекта нет.</param>
    /// <param name="documentsByName">Набор импортируемых документов для резолва имён компонентов.</param>
    public static ReImportAction Decide(
        CadDocument incoming,
        ExistingObjectSnapshot? existing,
        IReadOnlyDictionary<string, CadDocument> documentsByName)
    {
        if (existing is null)
            return ReImportAction.CreateObject;

        if (existing.Type != incoming.Type)
            return ReImportAction.TypeConflict;

        // Все версии аннулированы — сравнивать не с чем, создаём новую «В работе».
        if (existing.CurrentState is null)
            return ReImportAction.CreateNewVersion;

        if (IsUnchanged(incoming, existing, documentsByName))
            return ReImportAction.Unchanged;

        return existing.CurrentState == ObjectState.InWork
            ? ReImportAction.UpdateInWorkVersion
            : ReImportAction.CreateNewVersion;
    }

    /// <summary>«Данные не изменились»: наименование, материал, масса и состав.
    /// Состав сравнивается как мультимножество пар (идентичность потомка, количество) —
    /// порядок компонентов в JSON семантики не несёт (ADR 0007).</summary>
    private static bool IsUnchanged(
        CadDocument incoming,
        ExistingObjectSnapshot existing,
        IReadOnlyDictionary<string, CadDocument> documentsByName)
    {
        if (!string.Equals(incoming.Name, existing.Name, StringComparison.Ordinal)) return false;
        if (!string.Equals(incoming.Material, existing.Material, StringComparison.Ordinal)) return false;
        if (incoming.MassKg != existing.MassKg) return false;

        var incomingComposition = incoming.Components
            .Select(c =>
            {
                var child = documentsByName[c.FileName]; 
                return (Key: ObjectIdentity.Key(child.Type, child.Designation, child.Name), Count: c.Count);
            })
            .OrderBy(x => x.Key, StringComparer.Ordinal);

        var existingComposition = existing.Composition
            .Select(ch => (Key: ObjectIdentity.Key(ch.Type, ch.Designation, ch.Name), Count: ch.Quantity))
            .OrderBy(x => x.Key, StringComparer.Ordinal);

        return incomingComposition.SequenceEqual(existingComposition);
    }
}