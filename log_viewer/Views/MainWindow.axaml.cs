using Avalonia.Controls;
using Avalonia.Interactivity;

namespace log_viewer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    private async void OpenLog_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Log File",
            AllowMultiple = false,
            Filters =
            {
                new FileDialogFilter { Name = "Log Files", Extensions = { "log", "txt" } },
                new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
            }
        };

        var result = await dialog.ShowAsync(this);
        if (result is { Length: > 0 })
        {
            var filePath = result[0];
            // TODO: parse and display logs!
        }
    }
}

