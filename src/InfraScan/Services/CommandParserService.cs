using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using InfraScan.Models;

namespace InfraScan.Services
{
    public static class CommandParserService
    {
        public static ReportData Parse(Dictionary<string, string> rawOutputs, ServerConnection server)
        {
            var data = new ReportData
            {
                ServerDisplay = server.Host,
                OperatorName = server.OperatorName,
                Contract = server.Contract,
                Entity = server.Entity,
                Frequency = server.Frequency,
                ReportDate = DateTime.Now,
                RawOutputs = rawOutputs,
                ToolDescription = server.HasCockpitWeb
                    ? $"SSH y Cockpit web: {server.CockpitUrl}"
                    : "SSH"
            };

            if (rawOutputs.TryGetValue("Resumen del sistema", out var sysOut))
                ParseSystemSummary(sysOut, data);

            if (rawOutputs.TryGetValue("Rendimiento CPU", out var cpuOut))
                ParseCpu(cpuOut, data);

            if (rawOutputs.TryGetValue("Rendimiento memoria", out var memOut))
                ParseMemory(memOut, data);

            if (rawOutputs.TryGetValue("Rendimiento disco", out var diskOut))
                ParseDisk(diskOut, data);

            if (rawOutputs.TryGetValue("Red", out var netOut))
                ParseNetwork(netOut, data);

            if (rawOutputs.TryGetValue("Registros (Logs)", out var logOut))
                ParseLogs(logOut, data);

            if (rawOutputs.TryGetValue("Actualizaciones del sistema", out var updOut))
                ParseUpdates(updOut, data);

            if (rawOutputs.TryGetValue("Servicios", out var svcOut))
                ParseServices(svcOut, data);

            // Generate summary
            data.NovedadSummary = GenerateSummary(data);

            return data;
        }

        private static void ParseSystemSummary(string output, ReportData data)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var l = line.Trim();
                if (l.StartsWith("up ", StringComparison.OrdinalIgnoreCase) || l.Contains("up ") && l.Contains("hour"))
                    data.Uptime = l;
                else if (l.Contains("Static hostname:") || l.Contains("hostname:"))
                {
                    var match = Regex.Match(l, @"hostname:\s*(.+)", RegexOptions.IgnoreCase);
                    if (match.Success) data.Hostname = match.Groups[1].Value.Trim();
                }
                else if (l.Contains("PRETTY_NAME") || l.Contains("Operating System:"))
                {
                    var match = Regex.Match(l, @"(?:PRETTY_NAME=|Operating System:\s*)""?(.+?)""?\s*$");
                    if (match.Success) data.OSVersion = match.Groups[1].Value.Trim().Trim('"');
                }
                else if (l.Contains("QEMU") || l.Contains("VMware") || l.Contains("VirtualBox") || l.Contains("Standard PC"))
                    data.Model = l.Trim();
                else if (string.IsNullOrEmpty(data.OSVersion) && l.Contains("Linux") && l.Contains("Server"))
                    data.OSVersion = l.Trim();
            }

            if (string.IsNullOrEmpty(data.Hostname))
                data.Hostname = data.ServerDisplay;

            data.SystemStatus = "Sistema estable.";
            data.SystemActionRequired = false;
        }

        private static void ParseCpu(string output, ReportData data)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool nextIsLoad = false;
            bool nextIsCores = false;

            foreach (var line in lines)
            {
                var l = line.Trim();

                if (l == "LOAD:") { nextIsLoad = true; continue; }
                if (l == "CORES:") { nextIsCores = true; continue; }

                if (nextIsLoad)
                {
                    data.CpuLoad = l;
                    nextIsLoad = false;
                    continue;
                }
                if (nextIsCores)
                {
                    int.TryParse(l, out int cores);
                    data.CpuCores = cores;
                    nextIsCores = false;
                    continue;
                }

                // Parse top Cpu(s) line
                if (l.Contains("Cpu(s):") || l.Contains("%Cpu"))
                {
                    var usMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:us|%?\s*us)");
                    var syMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:sy|%?\s*sy)");
                    var niMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:ni|%?\s*ni)");
                    var idMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:id|%?\s*id)");
                    var waMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:wa|%?\s*wa)");
                    var hiMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:hi|%?\s*hi)");
                    var siMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:si|%?\s*si)");
                    var stMatch = Regex.Match(l, @"(\d+[\.,]\d+)\s*(?:st|%?\s*st)");

                    data.CpuUserPercent = ParseDouble(usMatch);
                    data.CpuSystemPercent = ParseDouble(syMatch);
                    data.CpuNicePercent = ParseDouble(niMatch);
                    data.CpuIdlePercent = ParseDouble(idMatch);
                    data.CpuIoWaitPercent = ParseDouble(waMatch);
                    data.CpuHwIrqPercent = ParseDouble(hiMatch);
                    data.CpuSwIrqPercent = ParseDouble(siMatch);
                    data.CpuStealPercent = ParseDouble(stMatch);
                }

                // Also try parsing load average from uptime-style output
                if (l.Contains("load average:"))
                {
                    var laMatch = Regex.Match(l, @"load average:\s*(\d+[\.,]\d+)");
                    if (laMatch.Success && string.IsNullOrEmpty(data.CpuLoad))
                        data.CpuLoad = laMatch.Groups[1].Value.Replace(',', '.');
                }
            }

            double usedCpu = 100.0 - data.CpuIdlePercent;
            if (usedCpu > 80)
            {
                data.CpuStatus = "Carga elevada";
                data.CpuActionRequired = true;
            }
            else
            {
                data.CpuStatus = "Dentro del umbral (< 80 %)";
                data.CpuActionRequired = false;
            }
        }

        private static void ParseMemory(string output, ReportData data)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var l = line.Trim();

                if (l.StartsWith("Mem:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = Regex.Split(l, @"\s+");
                    if (parts.Length >= 3)
                    {
                        data.MemoryTotal = parts[1];
                        data.MemoryUsed = parts[2];
                    }
                }
                else if (l.StartsWith("Swap:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = Regex.Split(l, @"\s+");
                    if (parts.Length >= 3)
                        data.SwapUsed = parts[2];
                }
            }

            // Calculate memory usage percentage
            double memUsedGb = ParseSizeToGb(data.MemoryUsed);
            double memTotalGb = ParseSizeToGb(data.MemoryTotal);
            double memPercent = memTotalGb > 0 ? (memUsedGb / memTotalGb) * 100 : 0;

            if (memPercent > 80)
            {
                data.MemoryStatus = "Uso de memoria elevado";
                data.MemoryActionRequired = true;
            }
            else if (data.SwapUsed != "0B" && data.SwapUsed != "0" && !string.IsNullOrEmpty(data.SwapUsed))
            {
                data.MemoryStatus = "Sin cuellos de botella";
                data.MemoryActionRequired = false;
            }
            else
            {
                data.MemoryStatus = "Sin cuellos de botella";
                data.MemoryActionRequired = false;
            }
        }

        private static void ParseDisk(string output, ReportData data)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var l = line.Trim();
                if (l.StartsWith("Filesystem") || l.StartsWith("S.ficheros")) continue;

                var parts = Regex.Split(l, @"\s+");
                if (parts.Length >= 5)
                {
                    // Look for the root partition or largest
                    string mountPoint = parts.Length >= 6 ? parts[5] : parts[parts.Length - 1];
                    if (mountPoint == "/" || string.IsNullOrEmpty(data.DiskTotal))
                    {
                        data.DiskTotal = parts[1];
                        data.DiskUsed = parts[2];
                        string percentStr = parts[4].TrimEnd('%');
                        int.TryParse(percentStr, out int percent);
                        data.DiskPercent = percent;
                    }
                }
            }

            if (data.DiskPercent >= 90)
            {
                data.DiskStatus = "¡Espacio crítico!";
                data.DiskActionRequired = true;
            }
            else if (data.DiskPercent >= 80)
            {
                data.DiskStatus = "Vigilar uso de disco";
                data.DiskActionRequired = true;
            }
            else
            {
                data.DiskStatus = "Sin alerta de espacio";
                data.DiskActionRequired = false;
            }
        }

        private static void ParseNetwork(string output, ReportData data)
        {
            var l = output.Trim();
            int.TryParse(l, out int connections);
            data.NetworkConnections = connections;
            data.NetworkStatus = "Sin anomalías";
            data.NetworkActionRequired = false;
        }

        private static void ParseLogs(string output, ReportData data)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool nextIsSshFails = false;
            bool nextIsCritical = false;

            foreach (var line in lines)
            {
                var l = line.Trim();

                if (l == "SSH_FAILS:") { nextIsSshFails = true; continue; }
                if (l == "CRITICAL:") { nextIsCritical = true; continue; }
                if (l == "ERRORS:") { break; }

                if (nextIsSshFails)
                {
                    int.TryParse(l, out int fails);
                    data.SshFailedAttempts = fails;
                    nextIsSshFails = false;
                }
                else if (nextIsCritical)
                {
                    int.TryParse(l, out int critical);
                    data.CriticalErrors = critical;
                    nextIsCritical = false;
                }
            }

            if (data.CriticalErrors > 0)
            {
                data.LogStatus = $"{data.CriticalErrors} errores críticos detectados";
                data.LogObservation = "fallos de\nautenticación\nregistrados";
            }
            else
            {
                data.LogStatus = "Sin errores críticos";
                data.LogObservation = "fallos de\nautenticación\nregistrados";
            }

            data.LogActionRequired = data.SshFailedAttempts > 1000 || data.CriticalErrors > 10;
        }

        private static void ParseUpdates(string output, ReportData data)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool nextIsUpdates = false;
            bool nextIsSecurity = false;

            foreach (var line in lines)
            {
                var l = line.Trim();

                if (l == "UPDATES:") { nextIsUpdates = true; continue; }
                if (l == "SECURITY:") { nextIsSecurity = true; continue; }

                if (nextIsUpdates)
                {
                    int.TryParse(l, out int updates);
                    data.TotalUpdates = updates;
                    nextIsUpdates = false;
                }
                else if (nextIsSecurity)
                {
                    int.TryParse(l, out int sec);
                    data.SecurityUpdates = sec;
                    nextIsSecurity = false;
                }
            }

            if (data.TotalUpdates > 0)
            {
                data.UpdateStatus = $"{data.TotalUpdates} actualizaciones disponibles, incluyendo {data.SecurityUpdates} de seguridad";
                data.UpdateActionRequired = data.SecurityUpdates > 0;
            }
            else
            {
                data.UpdateStatus = "Sin actualizaciones pendientes";
                data.UpdateActionRequired = false;
            }
        }

        private static void ParseServices(string output, ReportData data)
        {
            data.ListeningPorts = $"Puertos escuchando: {output.Trim()}";
            data.ServiceStatus = "Servicios clave\noperativos";
            data.ServiceAction = "Ninguna";
        }

        private static string GenerateSummary(ReportData data)
        {
            var parts = new List<string>();

            if (!data.CpuActionRequired && !data.MemoryActionRequired && !data.DiskActionRequired)
                parts.Add("El sistema funciona correctamente");

            if (data.UpdateActionRequired)
                parts.Add("se realizaron las actualizaciones pendientes y de seguridad");
            else if (data.TotalUpdates > 0)
                parts.Add($"hay {data.TotalUpdates} actualizaciones disponibles");

            if (data.CpuActionRequired) parts.Add("CPU con carga elevada");
            if (data.DiskActionRequired) parts.Add($"disco al {data.DiskPercent}%");
            if (data.MemoryActionRequired) parts.Add("memoria con uso elevado");

            return parts.Count > 0 ? string.Join(", ", parts) : "El sistema funciona correctamente";
        }

        // Helpers
        private static double ParseDouble(Match match)
        {
            if (!match.Success) return 0;
            string val = match.Groups[1].Value.Replace(',', '.');
            double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double result);
            return result;
        }

        private static double ParseSizeToGb(string size)
        {
            if (string.IsNullOrEmpty(size)) return 0;
            size = size.Replace(',', '.');

            var match = Regex.Match(size, @"(\d+[\.]?\d*)\s*(T|Ti|G|Gi|M|Mi|K|Ki|B)?", RegexOptions.IgnoreCase);
            if (!match.Success) return 0;

            double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val);
            string unit = match.Groups[2].Value.ToUpperInvariant();

            return unit switch
            {
                "T" or "TI" => val * 1024,
                "G" or "GI" => val,
                "M" or "MI" => val / 1024,
                "K" or "KI" => val / (1024 * 1024),
                _ => val
            };
        }
    }
}
