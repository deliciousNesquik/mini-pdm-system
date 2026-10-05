using Avalonia.Controls;
using MiniPdm.App.ViewModels;

namespace MiniPdm.App.Views;

public partial class SummaryWindow : Window
{
    public SummaryWindow()
    {
        InitializeComponent();
    }

    public SummaryViewModel ViewModel
    {
        get => (SummaryViewModel)DataContext!;
        set => DataContext = value;
    }

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}