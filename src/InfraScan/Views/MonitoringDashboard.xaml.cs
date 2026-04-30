using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using InfraScan.ViewModels;

namespace InfraScan.Views
{
    [SupportedOSPlatform("windows")]
    public partial class MonitoringDashboard : Window
    {
        private readonly MonitoringViewModel _vm;

        // Available refresh intervals in minutes
        private static readonly int[] Intervals = { 1, 2, 3, 5, 10, 15, 30 };

        public MonitoringDashboard()
        {
            InitializeComponent();
            _vm = new MonitoringViewModel();
            DataContext = _vm;

            // Populate interval ComboBox
            foreach (var min in Intervals)
                IntervalCombo.Items.Add(new ComboBoxItem
                {
                    Content = min == 1 ? "1 minuto" : $"{min} minutos",
                    Tag = min
                });
            IntervalCombo.SelectedIndex = 0; // default: 1 min
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
            => await _vm.RefreshAllAsync();

        // ── Filter handlers ─────────────────────────────────────────
        private void FilterAll_Click(object sender, RoutedEventArgs e)
            => _vm.ActiveFilter = MonitorFilter.All;

        private void FilterOnline_Click(object sender, RoutedEventArgs e)
            => _vm.ActiveFilter = _vm.ActiveFilter == MonitorFilter.OnlineOnly
                ? MonitorFilter.All
                : MonitorFilter.OnlineOnly;

        private void FilterOffline_Click(object sender, RoutedEventArgs e)
            => _vm.ActiveFilter = _vm.ActiveFilter == MonitorFilter.OfflineOnly
                ? MonitorFilter.All
                : MonitorFilter.OfflineOnly;

        // ── Interval handler ─────────────────────────────────────────
        private void IntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntervalCombo.SelectedItem is ComboBoxItem item && item.Tag is int minutes)
                _vm.RefreshIntervalMinutes = minutes;
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _vm.Dispose();
            base.OnClosed(e);
        }
    }
}
