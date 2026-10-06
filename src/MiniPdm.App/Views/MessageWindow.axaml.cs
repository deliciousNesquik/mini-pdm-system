using Avalonia.Controls;

namespace MiniPdm.App.Views;

public partial class MessageWindow : Window
{
    public MessageWindow(string title, string text)
    {
        InitializeComponent();
        Title = title;
        TextBlock.Text = text;
        OkButton.Click += (_, _) => Close();
    }
}