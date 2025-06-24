using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Material.Icons.Avalonia;

namespace Equalizer;

public partial class MessageBoxWindow : Window
{
    public MessageBoxWindow()
    {
        InitializeComponent();
    }
    public MessageBoxWindow(string title,string message, Material.Icons.MaterialIconKind iconName): this()
    {
        Title = title;
        MessageTextBlock.Text = message;
        MessageIcon.Kind = iconName;
    }

    private void ButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}