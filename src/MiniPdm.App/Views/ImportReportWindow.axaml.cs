using Avalonia.Controls;
using MiniPdm.App.ViewModels;

namespace MiniPdm.App.Views;

public partial class ImportReportWindow : Window
{
    public ImportReportWindow()
    {
        InitializeComponent();
    }

    public ImportReportViewModel ViewModel
    {
        get => (ImportReportViewModel)DataContext!;
        set => DataContext = value;
    }

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}