using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Import;

/// <summary>
/// Чистый анализатор набора CAD-документов отделяет импортируемые документы от отклоняемых и формирует причины.
/// </summary>
public sealed class ImportAnalyzer
{
    public ImportAnalysis Analyze(IReadOnlyList<CadDocument> documents)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var warnings = new Dictionary<string, string>(StringComparer.Ordinal);

        // Индекс по идентификатору документа, дубль имени отклоняет все копии
        var docs = new Dictionary<string, CadDocument>(StringComparer.Ordinal);
        foreach (var doc in documents)
        {
            if (docs.TryAdd(doc.FileName, doc)) continue;
            AddError(errors, doc.FileName, $"дублирующийся идентификатор документа «{doc.FileName}»");
            AddError(errors, docs[doc.FileName].FileName, $"дублирующийся идентификатор документа «{doc.FileName}»");
        }

        // Атрибуты документа по типу
        foreach (var doc in docs.Values)
            ValidateAttributes(doc, errors, warnings);

        // Дубли обозначений и наименований стандартных изделий
        MarkDuplicates(docs.Values, errors);

        // Состав сборок - количество, повторы, ссылки на отсутствующие документы.
        foreach (var doc in docs.Values)
            if (doc.Type == ObjectType.Assembly)
                ValidateComponents(doc, docs, errors);

        // Циклы среди еще не отклоненных а вот участники отклоняются.
        var rejected = errors.Keys.ToHashSet(StringComparer.Ordinal);
        var cycles = FindCycles(docs.Values.Where(d => !rejected.Contains(d.FileName)).ToList(), rejected);
        foreach (var cycle in cycles)
            foreach (var name in cycle)
                AddError(errors, name, $"циклическая ссылка: {string.Join(" -> ", cycle.Append(cycle[0]))}");

        // Каскад сборка с отклоненным компонентом отклоняется, вверх по дереву.
        PropagateRejection(docs.Values, errors);

        // Предупреждение отклоненному файлу не нужно — он и так не импортируется.
        foreach (var fileName in errors.Keys)
            warnings.Remove(fileName);

        var importable = docs.Values
            .Where(d => !errors.ContainsKey(d.FileName))
            .OrderBy(d => d.FileName, StringComparer.Ordinal)
            .ToList();

        var issues = errors
            .SelectMany(kv => kv.Value.Select(reason => new ImportIssue(kv.Key, ImportSeverity.Error, reason)))
            .Concat(warnings.Select(kv => new ImportIssue(kv.Key, ImportSeverity.Warning, kv.Value)))
            .OrderBy(i => i.FileName, StringComparer.Ordinal)
            .ThenBy(i => i.Severity)
            .ToList();

        return new ImportAnalysis(importable, issues, cycles);
    }

    private static void ValidateAttributes(
        CadDocument doc,
        Dictionary<string, List<string>> errors,
        Dictionary<string, string> warnings)
    {
        switch (doc.Type)
        {
            case ObjectType.Assembly:
            case ObjectType.Part:
                if (doc.Designation is null)
                    AddError(errors, doc.FileName, "не указано обозначение");
                else if (!DesignationRules.IsValid(doc.Designation))
                    AddError(errors, doc.FileName, $"обозначение «{doc.Designation}» не соответствует формату");
                break;

            case ObjectType.StandardPart:
                if (doc.Designation is not null)
                    AddError(errors, doc.FileName, "стандартное изделие не должно иметь обозначение");
                break;
        }

        if (doc.Type == ObjectType.Part && string.IsNullOrWhiteSpace(doc.Material))
            AddError(errors, doc.FileName, "не указан материал");

        // Масса отсутствует у детали или стандартного изделия - принимаем с предупреждением.
        // У сборки собственная масса не задается вообще, здесь она игнорируется.
        if (doc.Type != ObjectType.Assembly)
        {
            if (doc.MassKg is null)
                warnings.TryAdd(doc.FileName, "не указана масса");
            else if (doc.MassKg <= 0m)
                AddError(errors, doc.FileName, $"масса должна быть положительной, а не {doc.MassKg}");
        }
        
        if (doc.Type != ObjectType.Assembly && doc.Components.Count > 0)
            AddError(errors, doc.FileName, "состав есть, но тип документа не Assembly");
    }

    private static void MarkDuplicates(IEnumerable<CadDocument> docs, Dictionary<string, List<string>> errors)
    {
        foreach (var group in docs
                     .Where(d => d.Designation is not null)
                     .GroupBy(d => d.Designation!, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            var names = group.Select(d => d.FileName).OrderBy(n => n, StringComparer.Ordinal).ToList();
            foreach (var doc in group)
                AddError(errors, doc.FileName, $"обозначение «{group.Key}» дублируется в: {string.Join(", ", names)}");
        }

        foreach (var group in docs
                     .Where(d => d.Type == ObjectType.StandardPart)
                     .GroupBy(d => d.Name, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            var names = group.Select(d => d.FileName).OrderBy(n => n, StringComparer.Ordinal).ToList();
            foreach (var doc in group)
                AddError(errors, doc.FileName, $"наименование \"{group.Key}\" дублируется в: {string.Join(", ", names)}");
        }
    }

    private static void ValidateComponents(
        CadDocument doc,
        Dictionary<string, CadDocument> docs,
        Dictionary<string, List<string>> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in doc.Components)
        {
            if (component.Count <= 0)
            {
                AddError(errors, doc.FileName, $"у компонента \"{component.FileName}\" количество {component.Count} не больше нуля");
                continue;
            }

            if (!seen.Add(component.FileName))
            {
                AddError(errors, doc.FileName, $"компонент \"{component.FileName}\" указан в составе повторно");
                continue;
            }

            if (!docs.ContainsKey(component.FileName))
                AddError(errors, doc.FileName, $"ссылка на отсутствующий документ \"{component.FileName}\"");
        }
    }

    private static void PropagateRejection(IEnumerable<CadDocument> docs, Dictionary<string, List<string>> errors)
    {
        var rejected = errors.Keys.ToHashSet(StringComparer.Ordinal);
        var byName = docs.ToDictionary(d => d.FileName, StringComparer.Ordinal);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var doc in byName.Values)
            {
                if (doc.Type != ObjectType.Assembly || rejected.Contains(doc.FileName)) continue;

                var badComponent = doc.Components
                    .Select(c => c.FileName)
                    .FirstOrDefault(rejected.Contains);
                if (badComponent is null) continue;

                AddError(errors, doc.FileName, $"компонент \"{badComponent}\" отклонён");
                rejected.Add(doc.FileName);
                changed = true;
            }
        }
    }

    private static List<IReadOnlyList<string>> FindCycles(IReadOnlyList<CadDocument> docs, HashSet<string> rejected)
    {
        var names = docs.Select(d => d.FileName).ToHashSet(StringComparer.Ordinal);
        var graph = docs.ToDictionary(
            d => d.FileName,
            d => d.Components.Select(c => c.FileName)
                .Where(n => names.Contains(n) && !rejected.Contains(n))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            StringComparer.Ordinal);

        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 - поиск, 2 - обработан
        var path = new List<string>();
        var cycles = new List<IReadOnlyList<string>>();

        foreach (var start in graph.Keys.OrderBy(n => n, StringComparer.Ordinal))
            if (!state.ContainsKey(start))
                Dfs(start);

        return cycles;

        void Dfs(string node)
        {
            state[node] = 1;
            path.Add(node);
            foreach (var next in graph[node])
            {
                if (!state.TryGetValue(next, out var s))
                {
                    Dfs(next);
                }
                else if (s == 1)
                {
                    var startIdx = path.IndexOf(next);
                    cycles.Add(NormalizeCycle(path.GetRange(startIdx, path.Count - startIdx)));
                }
            }
            path.RemoveAt(path.Count - 1);
            state[node] = 2;
        }
    }

    /// <summary>Каноническая форма, цикл начинается с минимального имени (Ordinal) -
    /// один и тот же цикл не попадет в отчет дважды с разных стартов.</summary>
    private static IReadOnlyList<string> NormalizeCycle(List<string> cycle)
    {
        var minIdx = 0;
        for (var i = 1; i < cycle.Count; i++)
            if (string.CompareOrdinal(cycle[i], cycle[minIdx]) < 0)
                minIdx = i;
        return cycle.Skip(minIdx).Concat(cycle.Take(minIdx)).ToList();
    }
    
    private static void AddError(Dictionary<string, List<string>> errors, string fileName, string reason)
    {
        if (!errors.TryGetValue(fileName, out var list))
            errors[fileName] = list = [];
        if (!list.Contains(reason))
            list.Add(reason);
    }
}