using System.Windows;

namespace InfraScan;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Capture ALL unhandled exceptions and show the real message
        DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show(
                $"Error:\n\n{ex.Exception.GetType().Name}\n\n{ex.Exception.Message}\n\nStackTrace:\n{ex.Exception.StackTrace?[..Math.Min(800, ex.Exception.StackTrace?.Length ?? 0)]}",
                "Excepción no controlada", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
