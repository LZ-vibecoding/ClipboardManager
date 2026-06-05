using System.Windows;
using System.Windows.Input;

namespace ClipboardManager.Wpf.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Esc 关闭
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }
        base.OnKeyDown(e);
    }
}
