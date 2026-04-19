using System;
using System.Collections.Generic;

namespace InfraScan.Models
{
    public class ReportData
    {
        // Server info
        public string Hostname { get; set; } = string.Empty;
        public string ServerDisplay { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public string Contract { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string ToolDescription { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; } = DateTime.Now;

        // === Table data (parsed from commands) ===
        // System summary
        public string Uptime { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public string SystemStatus { get; set; } = "Sistema estable.";
        public bool SystemActionRequired { get; set; } = false;

        // CPU
        public string CpuLoad { get; set; } = string.Empty;
        public int CpuCores { get; set; }
        public double CpuUserPercent { get; set; }
        public double CpuSystemPercent { get; set; }
        public double CpuNicePercent { get; set; }
        public double CpuIdlePercent { get; set; }
        public double CpuIoWaitPercent { get; set; }
        public double CpuHwIrqPercent { get; set; }
        public double CpuSwIrqPercent { get; set; }
        public double CpuStealPercent { get; set; }
        public string CpuStatus { get; set; } = string.Empty;
        public bool CpuActionRequired { get; set; } = false;

        // Memory
        public string MemoryUsed { get; set; } = string.Empty;
        public string MemoryTotal { get; set; } = string.Empty;
        public string SwapUsed { get; set; } = string.Empty;
        public string MemoryStatus { get; set; } = string.Empty;
        public bool MemoryActionRequired { get; set; } = false;

        // Disk
        public string DiskUsed { get; set; } = string.Empty;
        public string DiskTotal { get; set; } = string.Empty;
        public int DiskPercent { get; set; }
        public string DiskStatus { get; set; } = string.Empty;
        public bool DiskActionRequired { get; set; } = false;

        // Network
        public int NetworkConnections { get; set; }
        public string NetworkStatus { get; set; } = "Sin anomalías";
        public bool NetworkActionRequired { get; set; } = false;

        // Logs
        public int SshFailedAttempts { get; set; }
        public int CriticalErrors { get; set; }
        public string LogStatus { get; set; } = string.Empty;
        public string LogObservation { get; set; } = string.Empty;
        public bool LogActionRequired { get; set; } = false;

        // Updates
        public int TotalUpdates { get; set; }
        public int SecurityUpdates { get; set; }
        public string UpdateStatus { get; set; } = string.Empty;
        public bool UpdateActionRequired { get; set; } = false;

        // Services
        public string ListeningPorts { get; set; } = string.Empty;
        public string ServiceStatus { get; set; } = "Servicios clave\noperativos";
        public string ServiceAction { get; set; } = "Ninguna";

        // === Output images (terminal screenshots) ===
        public List<OutputImage> OutputImages { get; set; } = new();

        // === Cockpit screenshots (optional) ===
        public byte[]? CockpitOverviewScreenshot { get; set; }
        public byte[]? CockpitMetricsScreenshot { get; set; }

        // Summary for final table
        public string NovedadSummary { get; set; } = string.Empty;

        // Raw command outputs for logging
        public Dictionary<string, string> RawOutputs { get; set; } = new();
    }

    public class OutputImage
    {
        public string Title { get; set; } = string.Empty;
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public int Order { get; set; }
    }
}
