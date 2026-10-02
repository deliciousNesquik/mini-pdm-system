using System.Text;
using MiniPdm.Cad.Json;
using MiniPdm.Core.Cad;

namespace MiniPdm.Tests.Cad.Json;

/// <summary>Оракул по реальному набору то есть читаются все, кроме Отдушины.
/// Требует запуска из репозитория (data/ поиск идет от bin).</summary>
[Trait("Category", "UsesDataset")]
public sealed class DatasetOracleTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "cad-export")))
            dir = dir.Parent;
        return dir?.FullName
               ?? throw new InvalidOperationException("data/cad-export не найдена — запустите тесты из репозитория");
    }

    [Fact]
    public async Task Dataset_reads_with_exactly_one_failure()
    {
        var reader = new JsonCadDocumentReader();
        var folder = Path.Combine(FindRepoRoot(), "data", "cad-export");

        var paths = await reader.ListDocumentsAsync(folder, CancellationToken.None);

        Assert.Equal(45, paths.Count); // 45 документов, README.txt отфильтрован

        var failures = new List<(string Name, CadDocumentException Error)>();
        var documents = new List<CadDocument>();
        foreach (var path in paths)
        {
            try { documents.Add(await reader.ReadAsync(path, CancellationToken.None)); }
            catch (CadDocumentException e) { failures.Add((Path.GetFileName(path), e)); }
        }

        var failure = Assert.Single(failures);
        Assert.Equal("Отдушина.m3d".Normalize(NormalizationForm.FormC), failure.Name.Normalize(NormalizationForm.FormC));
        Assert.Equal(44, documents.Count);
    }
}