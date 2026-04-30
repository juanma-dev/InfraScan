using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using InfraScan.Models;
using InfraScan.Services;

namespace InfraScan.ViewModels
{
    public enum MonitorFilter { All, OnlineOnly, OfflineOnly }

    [SupportedOSPlatform("windows")]
    public class MonitoringViewModel : INotifyPropertyChanged
    {
        private readonly StorageService _storage;
        private readonly DispatcherTimer _timer;
        private CancellationTokenSource _cts = new();
        private static readonly Random _rng = new();

        private string _searchText = string.Empty;
        private bool _isRefreshing;
        private string _statusText = "Listo";
        private int _countdown;
        private int _onlineCount;
        private int _offlineCount;
        private int _refreshIntervalMinutes = 1;
        private MonitorFilter _activeFilter = MonitorFilter.All;

        public ObservableCollection<ServerMetrics> AllMetrics      { get; } = new();
        public ObservableCollection<ServerMetrics> FilteredMetrics { get; } = new();

        // ── Filter ──────────────────────────────────────────────────
        public MonitorFilter ActiveFilter
        {
            get => _activeFilter;
            set
            {
                _activeFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterOnline));
                OnPropertyChanged(nameof(IsFilterOffline));
                ApplyFilter();
            }
        }
        public bool IsFilterAll     => _activeFilter == MonitorFilter.All;
        public bool IsFilterOnline  => _activeFilter == MonitorFilter.OnlineOnly;
        public bool IsFilterOffline => _activeFilter == MonitorFilter.OfflineOnly;

        // ── Interval ─────────────────────────────────────────────────
        public int RefreshIntervalMinutes
        {
            get => _refreshIntervalMinutes;
            set
            {
                if (value < 1) value = 1;
                _refreshIntervalMinutes = value;
                OnPropertyChanged();
                Countdown = _refreshIntervalMinutes * 60;
            }
        }

        // ── Search ───────────────────────────────────────────────────
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        // ── State ────────────────────────────────────────────────────
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotRefreshing)); }
        }
        public bool IsNotRefreshing => !_isRefreshing;

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public int Countdown
        {
            get => _countdown;
            set { _countdown = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountdownDisplay)); }
        }
        public string CountdownDisplay => $"Próxima actualización: {Countdown}s";

        public int OnlineCount
        {
            get => _onlineCount;
            set { _onlineCount = value; OnPropertyChanged(); }
        }
        public int OfflineCount
        {
            get => _offlineCount;
            set { _offlineCount = value; OnPropertyChanged(); }
        }

        // ── Constructor ──────────────────────────────────────────────
        public MonitoringViewModel()
        {
            _storage = new StorageService();
            Countdown = _refreshIntervalMinutes * 60;
            InitMetrics();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTimerTick;
            _timer.Start();

            _ = RefreshAllAsync();
        }

        private void InitMetrics()
        {
            AllMetrics.Clear();
            var servers = _storage.LoadServers();
            foreach (var s in servers)
                AllMetrics.Add(new ServerMetrics { ServerId = s.Id, ServerName = s.DisplayName, Host = s.Host, IsRefreshing = true });
            ApplyFilter();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            Countdown--;
            if (Countdown <= 0)
            {
                Countdown = _refreshIntervalMinutes * 60;
                _ = RefreshAllAsync();
            }
        }

        public async Task RefreshAllAsync()
        {
            if (IsRefreshing) return;

            IsRefreshing = true;
            Countdown = _refreshIntervalMinutes * 60;
            StatusText = "Actualizando métricas...";

            var servers = _storage.LoadServers();
            SyncPlaceholders(servers);

            _cts.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            int totalServers = servers.Count;
            // Stagger window: spread across 70% of the interval so all finish before next cycle
            int staggerWindowMs = (int)(_refreshIntervalMinutes * 60 * 0.70 * 1000);

            var tasks = servers.Select(async s =>
            {
                var placeholder = AllMetrics.FirstOrDefault(m => m.ServerId == s.Id);
                if (placeholder != null)
                    placeholder.IsRefreshing = true;

                // Random delay within stagger window to avoid network congestion
                if (totalServers > 1 && staggerWindowMs > 0)
                {
                    int delay = _rng.Next(0, staggerWindowMs);
                    try { await Task.Delay(delay, token); }
                    catch (OperationCanceledException) { return; }
                }

                if (token.IsCancellationRequested) return;

                var metrics = await MonitoringService.CollectMetricsAsync(s);

                if (token.IsCancellationRequested) return;

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var existing = AllMetrics.FirstOrDefault(m => m.ServerId == s.Id);
                    if (existing == null) return;
                    existing.IsOnline        = metrics.IsOnline;
                    existing.CpuPercent      = metrics.CpuPercent;
                    existing.RamUsedMB       = metrics.RamUsedMB;
                    existing.RamTotalMB      = metrics.RamTotalMB;
                    existing.DiskUsedGB      = metrics.DiskUsedGB;
                    existing.DiskTotalGB     = metrics.DiskTotalGB;
                    existing.Uptime          = metrics.Uptime;
                    existing.Load1m          = metrics.Load1m;
                    existing.Load5m          = metrics.Load5m;
                    existing.Load15m         = metrics.Load15m;
                    existing.ActiveConnections = metrics.ActiveConnections;
                    existing.CpuTempC        = metrics.CpuTempC;
                    existing.HasCpuTemp      = metrics.HasCpuTemp;
                    existing.KernelVersion   = metrics.KernelVersion;
                    existing.ProcessCount    = metrics.ProcessCount;
                    existing.LastUpdated     = metrics.LastUpdated;
                    existing.ErrorMessage    = metrics.ErrorMessage;
                    existing.IsRefreshing    = false;
                    UpdateCounts();
                    ApplyFilter();
                });
            });

            await Task.WhenAll(tasks);

            IsRefreshing = false;
            StatusText = $"Actualizado: {DateTime.Now:HH:mm:ss}  •  {OnlineCount} online  •  {OfflineCount} offline  •  Siguiente en {_refreshIntervalMinutes}min";
        }

        private void SyncPlaceholders(List<ServerConnection> servers)
        {
            foreach (var s in servers)
                if (!AllMetrics.Any(m => m.ServerId == s.Id))
                    AllMetrics.Add(new ServerMetrics { ServerId = s.Id, ServerName = s.DisplayName, Host = s.Host, IsRefreshing = true });

            var ids = servers.Select(s => s.Id).ToHashSet();
            foreach (var r in AllMetrics.Where(m => !ids.Contains(m.ServerId)).ToList())
                AllMetrics.Remove(r);

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredMetrics.Clear();

            IEnumerable<ServerMetrics> src = _activeFilter switch
            {
                MonitorFilter.OnlineOnly  => AllMetrics.Where(m => m.IsOnline && !m.IsRefreshing || m.IsRefreshing),
                MonitorFilter.OfflineOnly => AllMetrics.Where(m => !m.IsOnline && !m.IsRefreshing || m.IsRefreshing),
                _                         => AllMetrics
            };

            // Apply OnlineOnly/OfflineOnly only to non-refreshing items; always show refreshing
            if (_activeFilter == MonitorFilter.OnlineOnly)
                src = AllMetrics.Where(m => m.IsRefreshing || m.IsOnline);
            else if (_activeFilter == MonitorFilter.OfflineOnly)
                src = AllMetrics.Where(m => m.IsRefreshing || !m.IsOnline);

            var q = _searchText.Trim();
            if (!string.IsNullOrEmpty(q))
                src = src.Where(m =>
                    m.ServerName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    m.Host.Contains(q, StringComparison.OrdinalIgnoreCase));

            // Sort: online first, then by name
            src = src.OrderBy(m => m.IsOnline ? 0 : 1).ThenBy(m => m.ServerName);

            foreach (var m in src) FilteredMetrics.Add(m);
        }

        private void UpdateCounts()
        {
            OnlineCount  = AllMetrics.Count(m => !m.IsRefreshing && m.IsOnline);
            OfflineCount = AllMetrics.Count(m => !m.IsRefreshing && !m.IsOnline);
        }

        public void Dispose()
        {
            _timer.Stop();
            _cts.Cancel();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
