using System.Runtime.Versioning;
using System.Windows;
using InfraScan.Models;
using InfraScan.Services;

namespace InfraScan.Views
{
    [SupportedOSPlatform("windows")]
    public partial class ServerEditorDialog : Window
    {
        public ServerConnection? ServerResult { get; private set; }
        public bool ShouldGenerate { get; private set; }

        private readonly ServerConnection? _existing;

        public ServerEditorDialog(ServerConnection? existing = null)
        {
            InitializeComponent();
            _existing = existing;

            if (existing != null)
            {
                DialogTitle.Text = "Editar Servidor";
                Title = "Editar Servidor";
                TxtDisplayName.Text = existing.DisplayName;
                TxtHost.Text = existing.Host;
                TxtPort.Text = existing.Port.ToString();
                TxtUsername.Text = existing.Username;
                TxtOperator.Text = existing.OperatorName;
                TxtContract.Text = existing.Contract;
                TxtEntity.Text = existing.Entity;
                TxtFrequency.Text = existing.Frequency;
                ChkCockpit.IsChecked = existing.HasCockpitWeb;
                TxtCockpitUrl.Text = existing.CockpitUrl;
                TxtCockpitUser.Text = existing.CockpitUsername;

                // Don't show existing passwords, user must re-enter to change
            }
        }

        private bool ValidateFields()
        {
            if (string.IsNullOrWhiteSpace(TxtDisplayName.Text))
            {
                MessageBox.Show("El nombre para mostrar es requerido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDisplayName.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(TxtHost.Text))
            {
                MessageBox.Show("La IP/Hostname es requerida.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtHost.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(TxtUsername.Text))
            {
                MessageBox.Show("El usuario es requerido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUsername.Focus();
                return false;
            }
            if (_existing == null && string.IsNullOrEmpty(TxtPassword.Password))
            {
                MessageBox.Show("La contraseña es requerida.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPassword.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(TxtOperator.Text))
            {
                MessageBox.Show("El nombre del operador es requerido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtOperator.Focus();
                return false;
            }
            return true;
        }

        private ServerConnection BuildServer()
        {
            var server = _existing ?? new ServerConnection();
            server.DisplayName = TxtDisplayName.Text.Trim();
            server.Host = TxtHost.Text.Trim();
            int.TryParse(TxtPort.Text, out int port);
            server.Port = port > 0 ? port : 22;
            server.Username = TxtUsername.Text.Trim();
            server.OperatorName = TxtOperator.Text.Trim();
            server.Contract = TxtContract.Text.Trim();
            server.Entity = TxtEntity.Text.Trim();
            server.Frequency = TxtFrequency.Text.Trim();
            server.HasCockpitWeb = ChkCockpit.IsChecked == true;
            server.CockpitUrl = TxtCockpitUrl.Text.Trim();
            server.CockpitUsername = TxtCockpitUser.Text.Trim();

            // Only update password if provided (allows editing without re-entering)
            if (!string.IsNullOrEmpty(TxtPassword.Password))
                server.EncryptedPassword = CredentialService.Encrypt(TxtPassword.Password);

            if (!string.IsNullOrEmpty(TxtCockpitPassword.Password))
                server.CockpitEncryptedPassword = CredentialService.Encrypt(TxtCockpitPassword.Password);

            return server;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateFields()) return;
            ServerResult = BuildServer();
            ShouldGenerate = false;
            DialogResult = true;
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateFields()) return;
            ServerResult = BuildServer();
            ShouldGenerate = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ChkCockpit_Changed(object sender, RoutedEventArgs e)
        {
            CockpitPanel.Visibility = ChkCockpit.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Auto-fill Cockpit URL based on host
            if (ChkCockpit.IsChecked == true && string.IsNullOrEmpty(TxtCockpitUrl.Text) && !string.IsNullOrEmpty(TxtHost.Text))
            {
                TxtCockpitUrl.Text = $"https://{TxtHost.Text}:9090";
            }
        }
    }
}
