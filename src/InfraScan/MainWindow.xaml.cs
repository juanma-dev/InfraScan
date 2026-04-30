using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InfraScan.Models;
using InfraScan.ViewModels;
using InfraScan.Views;

namespace InfraScan
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            // Auto-scroll console
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ConsoleOutput))
                {
                    ConsoleScroller.ScrollToEnd();
                }
            };
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void AddServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ServerEditorDialog { Owner = this };
            if (dialog.ShowDialog() == true && dialog.ServerResult != null)
            {
                _vm.SaveServer(dialog.ServerResult);
            }
        }

        private void EditServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serverId)
            {
                var server = _vm.Servers.FirstOrDefault(s => s.Id == serverId);
                if (server == null) return;

                var dialog = new ServerEditorDialog(server) { Owner = this };
                if (dialog.ShowDialog() == true && dialog.ServerResult != null)
                {
                    _vm.SaveServer(dialog.ServerResult);
                }
            }
        }

        private void DeleteServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serverId)
            {
                var server = _vm.Servers.FirstOrDefault(s => s.Id == serverId);
                if (server != null)
                {
                    if (MessageBox.Show($"¿Desea eliminar de forma permanente el servidor '{server.DisplayName}'?", 
                        "Eliminar Servidor", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        _vm.DeleteServer(serverId);
                    }
                }
            }
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serverId)
            {
                var server = _vm.Servers.FirstOrDefault(s => s.Id == serverId);
                if (server != null)
                {
                    await _vm.GenerateReportAsync(server);
                }
            }
        }

        private void ServerCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Optional: select server on click
            if (sender is Border border && border.DataContext is ServerConnection server)
            {
                _vm.SelectedServer = server;
            }
        }

        private void OpenServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ServerBrowserDialog(_vm.Servers.ToList()) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                if (dialog.ActionType == "Edit" && dialog.SelectedServer != null)
                {
                    var editDialog = new ServerEditorDialog(dialog.SelectedServer) { Owner = this };
                    if (editDialog.ShowDialog() == true && editDialog.ServerResult != null)
                    {
                        _vm.SaveServer(editDialog.ServerResult);
                    }
                }
                else if (dialog.ActionType == "Generate" && dialog.SelectedServer != null)
                {
                    _ = _vm.GenerateReportAsync(dialog.SelectedServer);
                }
            }
        }

        private void Config_Click(object sender, RoutedEventArgs e)
        {
            var commands = _vm.GetCommands();
            var dialog = new CommandConfigDialog(commands) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _vm.SaveCommands(dialog.Commands);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            _vm.ConsoleOutput = "🖥️ InfraScan - Consola limpiada\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
        }

        private MonitoringDashboard? _monitoringWindow;
        private void OpenMonitoring_Click(object sender, RoutedEventArgs e)
        {
            if (_monitoringWindow == null || !_monitoringWindow.IsLoaded)
            {
                _monitoringWindow = new MonitoringDashboard();
                _monitoringWindow.Owner = this;
            }
            _monitoringWindow.Show();
            _monitoringWindow.Activate();
        }
    }
}