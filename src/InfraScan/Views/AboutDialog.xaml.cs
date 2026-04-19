using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;

namespace InfraScan.Views
{
    [SupportedOSPlatform("windows")]
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/juanma-dev/InfraScan.git")
            {
                UseShellExecute = true
            });
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
