using System.Text;
using MiniPdm.Core.Cad;

namespace MiniPdm.Tests.Data;

internal sealed class FakeCadDocumentReader : ICadDocumentReader
{
    private readonly IReadOnlyDictionary<string, CadDocument> _documents;
    private readonly HashSet<string> _broken;

    public FakeCadDocumentReader(
        IEnumerable<CadDocument> documents, IEnumerable<string>? brokenFileNames = null)
    {
        _documents = documents.ToDictionary(d => d.FileName, StringComparer.Ordinal);
        _broken = brokenFileNames?.ToHashSet(StringComparer.Ordinal) ?? [];
    }

    public Task<CadDocument> ReadAsync(string path, CancellationToken ct)
    {
        var name = Path.GetFileName(path).Normalize(NormalizationForm.FormC);
        if (_broken.Contains(name))
            throw new CadDocumentException($"Файл не является корректным JSON: {name}");

        return Task.FromResult(_documents.TryGetValue(name, out var doc)
            ? doc
            : throw new CadDocumentException($"Документ не найден: {name}"));
    }

    public Task<IReadOnlyList<string>> ListDocumentsAsync(string folder, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(
            _documents.Keys
                .Concat(_broken)                       // битые тоже попадают в перечисление
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => Path.Combine(folder, n))
                .ToList());
}