using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MiniPdm.App.Localization;
using MiniPdm.App.ViewModels;
using MiniPdm.App.Views;
using MiniPdm.Data;
using Serilog;

namespace MiniPdm.App;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Единый composition: контейнер Jab + делегат выбора папки из окна.
            var services = new AppServices();

            var window = new MainWindow
            {
                DataContext = new MainViewModel(
                    services.GetService<UiReadService>(),
                    services.GetService<ImportService>(),
                    services.GetService<PdmStateService>(),
                    pickFolder: () => Views.MainWindow.PickFolderAsync(desktop.MainWindow!))
            };

            Log.Information("Главное окно создано");
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}