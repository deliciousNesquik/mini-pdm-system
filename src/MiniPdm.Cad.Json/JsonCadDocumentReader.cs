using System.Text;
using System.Text.Json;
using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;

namespace MiniPdm.Cad.Json;

/// <summary>Реализация ICadDocumentReader поверх JSON-файлов.
/// Единственная точка нормализации имён (ADR 0005) и единственное место,
/// знающее о JSON (обязательная часть ТЗ, п.1).</summary>
public sealed class JsonCadDocumentReader : ICadDocumentReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false, // имена полей заданы атрибутами точно
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly JsonCadDocumentReaderOptions _options;

    public JsonCadDocumentReader(JsonCadDocumentReaderOptions? options = null) =>
        _options = options ?? new JsonCadDocumentReaderOptions();

    public async Task<CadDocument> ReadAsync(string path, CancellationToken ct)
    {
        CadFileDto dto;
        try
        {
            await using var stream = File.OpenRead(path);
            dto = await JsonSerializer.DeserializeAsync<CadFileDto>(stream, SerializerOptions, ct)
                  ?? throw new CadDocumentException($"Файл пуст: {path}");
        }
        catch (JsonException e)
        {
            throw new CadDocumentException($"Файл не является корректным JSON: {path}", e);
        }
        catch (IOException e)
        {
            // Файл исчез/не читается между перечислением и чтением — по контракту это «документ не читается».
            throw new CadDocumentException($"Файл не читается: {path}", e);
        }
        // OperationCanceledException намеренно не перехватывается: отмена — не свойство документа.

        if (dto.FormatVersion != 1)
            throw new CadDocumentException(
                $"Неподдерживаемая версия формата {dto.FormatVersion} (ожидается 1): {path}");

        if (dto.FileName is null || dto.Name is null || dto.Type is null)
            throw new CadDocumentException($"Отсутствуют обязательные поля fileName/name/type: {path}");

        // Идентификатор — имя файла НА ДИСКЕ (ТЗ). fileName из JSON — информационное,
        // сверяется после нормализации: расхождение = некорректный документ (ADR 0010).
        var diskName = Normalize(Path.GetFileName(path));
        var jsonName = Normalize(dto.FileName);
        if (!string.Equals(diskName, jsonName, StringComparison.Ordinal))
            throw new CadDocumentException(
                $"fileName «{jsonName}» не совпадает с именем файла на диске «{diskName}»: {path}");

        return new CadDocument
        {
            FileName = diskName,
            Type = ParseType(dto.Type, path),
            Designation = dto.Designation is null ? null : Normalize(dto.Designation),
            Name = Normalize(dto.Name), // наименование — идентичность StandardPart, нормализуем
            Material = dto.Properties?.Material, // данные, не идентификатор — не трогаем
            MassKg = dto.Properties?.Mass,
            Components = (dto.Components ?? [])
                .Select(c => new CadComponent(c.File is null ? "" : Normalize(c.File), c.Count))
                .ToList()
        };
    }

    public async Task<IReadOnlyList<string>> ListDocumentsAsync(string folder, CancellationToken ct)
    {
        await Task.CompletedTask; // перечисление синхронное; контракт — асинхронный ради будущих источников

        try
        {
            return Directory.EnumerateFiles(folder)
                .Where(f =>
                    f.EndsWith(".a3d", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".m3d", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal) // детерминированный порядок
                .ToList();
        }
        catch (Exception e) when (e is DirectoryNotFoundException or UnauthorizedAccessException)
        {
            throw new CadDocumentException($"Папка недоступна: {folder}", e);
        }
    }

    private static ObjectType ParseType(string raw, string path) => raw switch
    {
        "Assembly" => ObjectType.Assembly,
        "Part" => ObjectType.Part,
        "StandardPart" => ObjectType.StandardPart,
        _ => throw new CadDocumentException($"Неизвестный тип документа «{raw}»: {path}")
    };

    private string Normalize(string value) => value.Normalize(_options.NameNormalization);
}