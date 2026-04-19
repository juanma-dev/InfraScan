using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using InfraScan.Models;

namespace InfraScan.Views
{
    [SupportedOSPlatform("windows")]
    public partial class ServerBrowserDialog : Window
    {
        public ServerConnection? SelectedServer { get; private set; }
        public string ActionType { get; private set; } = "";
        private readonly List<ServerConnection> _servers;

        public ServerBrowserDialog(List<ServerConnection> servers)
        {
            InitializeComponent();
            _servers = servers;
            ServersList.ItemsSource = servers;
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                SelectedServer = _servers.FirstOrDefault(s => s.Id == id);
                ActionType = "Edit";
                DialogResult = true;
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                SelectedServer = _servers.FirstOrDefault(s => s.Id == id);
                ActionType = "Generate";
                DialogResult = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
