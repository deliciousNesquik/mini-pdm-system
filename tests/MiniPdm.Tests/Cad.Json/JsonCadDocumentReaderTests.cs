using System.Text;
using MiniPdm.Cad.Json;
using MiniPdm.Core.Cad;

namespace MiniPdm.Tests.Cad.Json;

public sealed class JsonCadDocumentReaderTests
{
    private readonly JsonCadDocumentReader _reader = new();

    private static async Task<string> WriteTempAsync(string fileName, string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "minipdm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8);
        return path;
    }

    private const string PartJson = """
        {
          "formatVersion": 1,
          "fileName": "Колесо зубчатое.m3d",
          "type": "Part",
          "designation": "РДЦЛ.304112.302",
          "name": "Колесо зубчатое",
          "properties": { "material": "Сталь 40Х", "mass": 5.86 },
          "components": []
        }
        """;

    [Fact]
    public async Task Reads_part_with_all_fields()
    {
        var path = await WriteTempAsync("Колесо зубчатое.m3d", PartJson);

        var doc = await _reader.ReadAsync(path, CancellationToken.None);

        Assert.Equal("Колесо зубчатое.m3d", doc.FileName);
        Assert.Equal(Core.Domain.ObjectType.Part, doc.Type);
        Assert.Equal("РДЦЛ.304112.302", doc.Designation);
        Assert.Equal("Сталь 40Х", doc.Material);
        Assert.Equal(5.86m, doc.MassKg);
        Assert.Empty(doc.Components);
    }

    [Fact]
    public async Task Reads_assembly_and_normalizes_component_names()
    {
        const string json = """
            {
              "formatVersion": 1,
              "fileName": "Узел.a3d",
              "type": "Assembly",
              "designation": "РДЦЛ.304112.200",
              "name": "Узел",
              "properties": { "material": null, "mass": null },
              "components": [ { "file": "Болт.m3d", "count": 4 } ]
            }
            """;
        var path = await WriteTempAsync("Узел.a3d", json);

        var doc = await _reader.ReadAsync(path, CancellationToken.None);

        var component = Assert.Single(doc.Components);
        Assert.Equal("Болт.m3d", component.FileName);
        Assert.Equal(4, component.Count);
    }

    [Fact]
    public async Task Nfd_name_on_disk_matches_nfc_in_json()   // ядро ADR 0005
    {
        // Файл на диске создаём с NFD-именем («й» как и + спец символ),
        // а fileName в JSON уже в NFC, как в наборе. После нормализации должны совпасть.
        const string json = """
            {
              "formatVersion": 1,
              "fileName": "Шайба упорная.m3d",
              "type": "StandardPart",
              "designation": null,
              "name": "Шайба упорная",
              "properties": { "material": null, "mass": 0.01 },
              "components": []
            }
            """;
        var nfdName = "Шайба упорная.m3d".Normalize(NormalizationForm.FormD);
        var path = await WriteTempAsync(nfdName, json);

        var doc = await _reader.ReadAsync(path, CancellationToken.None);

        Assert.Equal("Шайба упорная.m3d", doc.FileName); // NFC-форма в домене
    }

    [Fact]
    public async Task Broken_json_raises_reader_error()
    {
        const string broken = """
            {
              "formatVersion": 1,
              "fileName": "Отдушина.m3d",
              "type": "StandardPart"
            """; // обрезан
        var path = await WriteTempAsync("Отдушина.m3d", broken);

        var error = await Assert.ThrowsAsync<CadDocumentException>(
            () => _reader.ReadAsync(path, CancellationToken.None));
        Assert.Contains("JSON", error.Message);
    }

    [Fact]
    public async Task Unsupported_format_version_raises_error()
    {
        var path = await WriteTempAsync("X.m3d",
            PartJson.Replace("\"formatVersion\": 1", "\"formatVersion\": 2"));

        await Assert.ThrowsAsync<CadDocumentException>(
            () => _reader.ReadAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_type_raises_error()
    {
        var path = await WriteTempAsync("X.m3d",
            PartJson.Replace("\"type\": \"Part\"", "\"type\": \"Subassembly\""));

        await Assert.ThrowsAsync<CadDocumentException>(
            () => _reader.ReadAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task FileName_mismatch_after_normalization_raises_error()
    {
        var path = await WriteTempAsync("Одно.m3d",
            PartJson.Replace("Колесо зубчатое.m3d", "Другое.m3d"));

        await Assert.ThrowsAsync<CadDocumentException>(
            () => _reader.ReadAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task List_returns_only_document_paths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "minipdm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "Сборка.a3d"), "{}");
        await File.WriteAllTextAsync(Path.Combine(dir, "Деталь.m3d"), "{}");
        await File.WriteAllTextAsync(Path.Combine(dir, "README.txt"), "не документ");

        var paths = await _reader.ListDocumentsAsync(dir, CancellationToken.None);

        Assert.Equal(2, paths.Count);
        Assert.All(paths, p => Assert.True(Path.IsPathRooted(p))); // полные пути — ADR 0006
    }
}