using System;
using Avalonia.Markup.Xaml;

namespace MiniPdm.App.Localization;

/// <summary>
/// Расширение разметки для локализации строк в XAML.
/// </summary>
public sealed class LocalizeExtension : MarkupExtension
{
    /// <summary>
    /// Ключ для локализованной строки.
    /// </summary>
    public string Key { get; set; } = "";

    public LocalizeExtension() { }

    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}