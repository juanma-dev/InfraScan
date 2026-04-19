using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using InfraScan.Models;
using InfraScan.Services;

namespace InfraScan.ViewModels
{
    [SupportedOSPlatform("windows")]
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly StorageService _storage;
        private string _consoleOutput = "🖥️ InfraScan - Listo para conectar\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
        private bool _isWorking;
        private string _statusText = "Listo";
        private double _progress;
        private ServerConnection? _selectedServer;
        private CancellationTokenSource? _cts;

        public ObservableCollection<ServerConnection> Servers { get; } = new();

        public string ConsoleOutput
        {
            get => _consoleOutput;
            set { _consoleOutput = value; OnPropertyChanged(); }
        }

        public bool IsWorking
        {
            get => _isWorking;
            set { _isWorking = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotWorking)); }
        }

        public bool IsNotWorking => !_isWorking;

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public ServerConnection? SelectedServer
        {
            get => _selectedServer;
            set { _selectedServer = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _storage = new StorageService();
            LoadServers();
        }

        public void LoadServers()
        {
            Servers.Clear();
            foreach (var s in _storage.LoadServers())
                Servers.Add(s);
        }

        public void SaveServer(ServerConnection server)
        {
            _storage.SaveServer(server);
            LoadServers();
        }

        public void DeleteServer(string serverId)
        {
            _storage.DeleteServer(serverId);
            LoadServers();
        }

        public List<CommandConfig> GetCommands() => _storage.LoadCommands();

        public void SaveCommands(List<CommandConfig> commands) => _storage.SaveCommands(commands);

        public async Task GenerateReportAsync(ServerConnection server)
        {
            if (IsWorking) return;

            IsWorking = true;
            _cts = new CancellationTokenSource();
            Progress = 0;
            StatusText = "Conectando...";

            AppendLog($"\n\n🚀 GENERANDO INFORME PARA: {server.DisplayName} ({server.Host})");
            AppendLog($"   Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            AppendLog("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            try
            {
                var commands = _storage.LoadCommands();
                var tableCommands = commands.Where(c => c.Category == CommandCategory.Table).OrderBy(c => c.Order).ToList();
                var outputCommands = commands.Where(c => c.Category == CommandCategory.Output).OrderBy(c => c.Order).ToList();

                // Connect
                using var ssh = new SshService(server);
                ssh.OnLog += msg => Application.Current.Dispatcher.Invoke(() => AppendLog(msg));

                bool connected = await ssh.ConnectAsync();
                if (!connected)
                {
                    StatusText = "Error de conexión";
                    IsWorking = false;
                    return;
                }

                // Execute table commands
                StatusText = "Ejecutando comandos de diagnóstico...";
                var progressReporter = new Progress<(int current, int total, string name)>(p =>
                {
                    Progress = (double)p.current / p.total * 80;
                    StatusText = $"[{p.current}/{p.total}] {p.name}";
                });

                var allCommands = tableCommands.Concat(outputCommands).ToList();
                var rawOutputs = await ssh.ExecuteAllCommandsAsync(allCommands, progressReporter, _cts.Token);

                // Parse data
                StatusText = "Analizando datos...";
                Progress = 85;
                var reportData = CommandParserService.Parse(rawOutputs, server);

                // Generate terminal images for output commands
                StatusText = "Generando imágenes de terminal...";
                Progress = 88;
                int imgOrder = 1;
                foreach (var cmd in outputCommands)
                {
                    if (rawOutputs.TryGetValue(cmd.Name, out var output))
                    {
                        byte[] imgData = TerminalImageRenderer.RenderTerminalOutput(output, cmd.Name);
                        reportData.OutputImages.Add(new OutputImage
                        {
                            Title = cmd.Name,
                            ImageData = imgData,
                            Order = imgOrder++
                        });
                    }
                }

                // Cockpit screenshots (optional)
                if (server.HasCockpitWeb && !string.IsNullOrEmpty(server.CockpitUrl))
                {
                    StatusText = "Capturando Cockpit web...";
                    Progress = 90;
                    try
                    {
                        var cockpitService = new CockpitScreenshotService();
                        var cockpitUser = !string.IsNullOrEmpty(server.CockpitUsername)
                            ? server.CockpitUsername : server.Username;
                        var cockpitPwd = !string.IsNullOrEmpty(server.CockpitEncryptedPassword)
                            ? CredentialService.Decrypt(server.CockpitEncryptedPassword)
                            : CredentialService.Decrypt(server.EncryptedPassword);

                        var (overview, metrics) = await cockpitService.CaptureScreenshotsAsync(
                            server.CockpitUrl, cockpitUser, cockpitPwd);
                        reportData.CockpitOverviewScreenshot = overview;
                        reportData.CockpitMetricsScreenshot = metrics;
                        AppendLog("📸 Screenshots de Cockpit capturados");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"⚠️ No se pudieron capturar screenshots de Cockpit: {ex.Message}");
                    }
                }

                // Generate docx
                StatusText = "Generando informe .docx...";
                Progress = 95;
                var generator = new ReportGeneratorService();
                string filePath = generator.GenerateReport(reportData, outputCommands);

                // Update server last report date
                server.LastReportDate = DateTime.Now;
                _storage.SaveServer(server);
                LoadServers();

                Progress = 100;
                StatusText = "✅ Informe generado correctamente";

                AppendLog($"\n✅ INFORME GENERADO EXITOSAMENTE");
                AppendLog($"📁 Ubicación: {filePath}");
                AppendLog("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                // Ask to open
                var result = MessageBox.Show(
                    $"Informe generado exitosamente.\n\n📁 {filePath}\n\n¿Desea abrir el archivo?",
                    "Informe Generado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }

                ssh.Disconnect();
            }
            catch (OperationCanceledException)
            {
                StatusText = "Cancelado";
                AppendLog("⚠️ Operación cancelada por el usuario");
            }
            catch (Exception ex)
            {
                StatusText = "Error";
                AppendLog($"\n❌ ERROR: {ex.Message}");
                MessageBox.Show($"Error generando informe:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsWorking = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void CancelOperation()
        {
            _cts?.Cancel();
        }

        private void AppendLog(string message)
        {
            ConsoleOutput += message + "\n";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Simple RelayCommand
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
    }
}
