using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiniPdm.App.Views;

/// <summary>Простой модальный диалог «заголовок + текст + ОК».
/// Создаётся только кодом (не из XAML-дизайнера): параметры через конструктор.</summary>
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