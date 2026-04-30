using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace InfraScan.Models
{
    public class ServerMetrics : INotifyPropertyChanged
    {
        private bool _isOnline;
        private double _cpuPercent;
        private long _ramUsedMB;
        private long _ramTotalMB;
        private double _diskUsedGB;
        private double _diskTotalGB;
        private string _uptime = string.Empty;
        private double _load1m, _load5m, _load15m;
        private int _activeConnections;
        private double _cpuTempC;
        private bool _hasCpuTemp;
        private DateTime _lastUpdated;
        private string _errorMessage = string.Empty;
        private bool _isRefreshing;
        private string _kernelVersion = string.Empty;
        private int _processCount;

        public string ServerId { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        public double CpuPercent
        {
            get => _cpuPercent;
            set
            {
                _cpuPercent = Math.Min(100, Math.Max(0, value));
                OnPropertyChanged(); OnPropertyChanged(nameof(CpuDisplay)); OnPropertyChanged(nameof(CpuColor));
            }
        }

        public long RamUsedMB
        {
            get => _ramUsedMB;
            set { _ramUsedMB = value; OnPropertyChanged(); OnPropertyChanged(nameof(RamPercent)); OnPropertyChanged(nameof(RamDisplay)); OnPropertyChanged(nameof(RamColor)); }
        }

        public long RamTotalMB
        {
            get => _ramTotalMB;
            set { _ramTotalMB = value; OnPropertyChanged(); OnPropertyChanged(nameof(RamPercent)); OnPropertyChanged(nameof(RamDisplay)); }
        }

        public double DiskUsedGB
        {
            get => _diskUsedGB;
            set { _diskUsedGB = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskPercent)); OnPropertyChanged(nameof(DiskDisplay)); OnPropertyChanged(nameof(DiskColor)); }
        }

        public double DiskTotalGB
        {
            get => _diskTotalGB;
            set { _diskTotalGB = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskPercent)); OnPropertyChanged(nameof(DiskDisplay)); }
        }

        public string Uptime
        {
            get => _uptime;
            set { _uptime = value; OnPropertyChanged(); }
        }

        public double Load1m { get => _load1m; set { _load1m = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoadDisplay)); } }
        public double Load5m { get => _load5m; set { _load5m = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoadDisplay)); } }
        public double Load15m { get => _load15m; set { _load15m = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoadDisplay)); } }

        public int ActiveConnections
        {
            get => _activeConnections;
            set { _activeConnections = value; OnPropertyChanged(); }
        }

        public double CpuTempC
        {
            get => _cpuTempC;
            set { _cpuTempC = value; OnPropertyChanged(); OnPropertyChanged(nameof(TempDisplay)); }
        }

        public bool HasCpuTemp
        {
            get => _hasCpuTemp;
            set { _hasCpuTemp = value; OnPropertyChanged(); OnPropertyChanged(nameof(TempDisplay)); }
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set { _lastUpdated = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastUpdatedDisplay)); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        // Computed
        public double RamPercent => RamTotalMB > 0 ? (double)RamUsedMB / RamTotalMB * 100.0 : 0;
        public double DiskPercent => DiskTotalGB > 0 ? DiskUsedGB / DiskTotalGB * 100.0 : 0;

        public string CpuDisplay  => $"{CpuPercent:F1}%";
        public string RamDisplay  => RamTotalMB  > 0 ? $"{RamUsedMB  / 1024.0:F1} / {RamTotalMB  / 1024.0:F1} GB" : "N/A";
        public string DiskDisplay => DiskTotalGB > 0 ? $"{DiskUsedGB:F1} / {DiskTotalGB:F1} GB" : "N/A";
        public string TempDisplay => HasCpuTemp ? $"{CpuTempC:F0}°C" : "—";
        public string LoadDisplay => $"{Load1m:F2}  ·  {Load5m:F2}  ·  {Load15m:F2}";
        public string LastUpdatedDisplay => LastUpdated == default ? "Pendiente..." : $"Actualizado: {LastUpdated:HH:mm:ss}";

        // Short texts for gauge circles — percentage only so text fits
        public string RamGaugeText  => $"{RamPercent:F0}%";
        public string DiskGaugeText => $"{DiskPercent:F0}%";

        // Compact sub-label shown below each gauge
        public string RamSubLabel  => RamTotalMB  > 0 ? $"{RamUsedMB  / 1024.0:F1}/{RamTotalMB  / 1024.0:F0}GB" : "—";
        public string DiskSubLabel => DiskTotalGB > 0 ? $"{DiskUsedGB:F0}/{DiskTotalGB:F0}GB" : "—";

        public string KernelVersion
        {
            get => _kernelVersion;
            set { _kernelVersion = value; OnPropertyChanged(); }
        }

        public int ProcessCount
        {
            get => _processCount;
            set { _processCount = value; OnPropertyChanged(); }
        }

        public Color CpuColor => MetricColor(CpuPercent);
        public Color RamColor => MetricColor(RamPercent);
        public Color DiskColor => MetricColor(DiskPercent);
        public Color StatusColor => IsOnline ? Color.FromRgb(34, 197, 94) : Color.FromRgb(239, 68, 68);

        public static Color MetricColor(double pct)
        {
            if (pct >= 85) return Color.FromRgb(239, 68, 68);
            if (pct >= 70) return Color.FromRgb(245, 158, 11);
            return Color.FromRgb(34, 197, 94);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
