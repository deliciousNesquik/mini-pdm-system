using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MiniPdm.App.Localization;
using MiniPdm.App.ViewModels;
using MiniPdm.Core.Bom;
using MiniPdm.Core.Import;
using Serilog;

namespace MiniPdm.App.Views;

/// <summary>Главное окно. Code-behind минимальный по ТЗ: связывание событий VM
/// с диалогами/окнами и диалог выбора папки. Подписка — в OnDataContextChanged.</summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.MessageRequested -= OnMessageRequested;
            _viewModel.ImportCompleted -= OnImportCompleted;
            _viewModel.SummaryRequested -= OnSummaryRequested;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is null) return;

        _viewModel.MessageRequested += OnMessageRequested;
        _viewModel.ImportCompleted += OnImportCompleted;
        _viewModel.SummaryRequested += OnSummaryRequested;

        _ = _viewModel.InitializeAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is null) return;
        _viewModel.MessageRequested -= OnMessageRequested;
        _viewModel.ImportCompleted -= OnImportCompleted;
        _viewModel.SummaryRequested -= OnSummaryRequested;
    }

    public static async Task<string?> PickFolderAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.Import_Folder_Title,
            AllowMultiple = false
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async void OnMessageRequested(string title, string text)
    {
        try
        {
            await new MessageWindow(title, text).ShowDialog(this);
        }
        catch (Exception e)
        {
            Log.Error(e, "Не удалось показать диалог");
        }
    }

    private void OnImportCompleted(ImportAnalysis analysis) =>
        new ImportReportWindow { ViewModel = new ImportReportViewModel(analysis) }.ShowDialog(this);

    private void OnSummaryRequested(IReadOnlyList<SummaryLine> lines) =>
        new SummaryWindow { ViewModel = new SummaryViewModel(lines) }.ShowDialog(this);
}