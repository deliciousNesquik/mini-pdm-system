using Jab;
using MiniPdm.Cad.Json;
using MiniPdm.Core.Cad;
using MiniPdm.Data;

namespace MiniPdm.App;

/// <summary>
/// Регистрация сервисов приложения в контейнере внедрения зависимостей.
/// </summary>
[ServiceProvider]
[Singleton(typeof(ICadDocumentReader), typeof(JsonCadDocumentReader))]
[Singleton(typeof(Db), Factory = nameof(CreateDb))]
[Singleton(typeof(ImportService))]
[Singleton(typeof(VersionService))]
[Singleton(typeof(PdmObjectRepository))]
[Singleton(typeof(BomRepository))]
[Singleton(typeof(UiReadService))]
[Singleton(typeof(PdmStateService))]
internal partial class AppServices
{
    /// <summary>
    /// Настройки приложения, загруженные из appsettings.json
    /// </summary>
    private static readonly AppSettings Settings = AppSettingsLoader.Load();

    /// <summary>
    /// Создает экземпляр Db с использованием строки подключения из настроек приложения.
    /// </summary>
    /// <returns>Подключение к базе данных</returns>
    internal Db CreateDb() => new(Settings.ConnectionStrings.Default);
}