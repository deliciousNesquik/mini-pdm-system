using System.Text;

namespace MiniPdm.Cad.Json;

/// <summary>Настройки ридера. Точка смены формы нормализации (ADR 0005).</summary>
public sealed class JsonCadDocumentReaderOptions
{
    /// <summary>Форма нормализации имён документов. FormC (NFC) по ADR 0005. Данные набора пишутся в NFC</summary>
    public NormalizationForm NameNormalization { get; init; } = NormalizationForm.FormC;
}