using System;
using System.Globalization;
using System.Threading.Tasks;
using InfraScan.Models;
using Renci.SshNet;

namespace InfraScan.Services
{
    public static class MonitoringService
    {
        private const int ConnectTimeoutSec = 12;
        private const int CmdTimeoutSec = 10;

        public static async Task<ServerMetrics> CollectMetricsAsync(ServerConnection server)
        {
            var m = new ServerMetrics
            {
                ServerId = server.Id,
                ServerName = server.DisplayName,
                Host = server.Host
            };

            try
            {
                string password = CredentialService.Decrypt(server.EncryptedPassword);
                var ci = new ConnectionInfo(server.Host, server.Port, server.Username,
                    new PasswordAuthenticationMethod(server.Username, password))
                { Timeout = TimeSpan.FromSeconds(ConnectTimeoutSec) };

                using var client = new SshClient(ci);
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    m.IsOnline = false;
                    m.ErrorMessage = "Conexión rechazada";
                    return m;
                }

                m.IsOnline = true;

                // Launch all commands in parallel
                var tCpu  = RunAsync(client, "top -bn1 | grep 'Cpu(s)' | awk '{print $2+$4}'");
                var tRam  = RunAsync(client, "free -m | awk 'NR==2{print $2,$3}'");
                var tDisk = RunAsync(client, "df -BG / | awk 'NR==2{gsub(/G/,\"\",$2);gsub(/G/,\"\",$3);print $2,$3}'");
                var tUp   = RunAsync(client, "uptime -p 2>/dev/null || uptime | awk -F'up ' '{print $2}' | awk -F',' '{print $1,$2}'");
                var tLoad = RunAsync(client, "cat /proc/loadavg | awk '{print $1,$2,$3}'");
                var tConn = RunAsync(client, "ss -s 2>/dev/null | grep -i estab | awk '{print $4}' | tr -d ','");
                var tTemp = RunAsync(client, "cat /sys/class/thermal/thermal_zone0/temp 2>/dev/null | awk '{printf \"%.1f\",$1/1000}'");
                var tKern = RunAsync(client, "uname -r");
                var tProc = RunAsync(client, "ps aux --no-headers | wc -l");

                await Task.WhenAll(tCpu, tRam, tDisk, tUp, tLoad, tConn, tTemp, tKern, tProc);

                // CPU
                if (TryDouble(await tCpu, out double cpu)) m.CpuPercent = cpu;

                // RAM
                var rp = (await tRam).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (rp.Length >= 2 && long.TryParse(rp[0], out long rt) && long.TryParse(rp[1], out long ru))
                { m.RamTotalMB = rt; m.RamUsedMB = ru; }

                // Disk
                var dp = (await tDisk).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (dp.Length >= 2 && TryDouble(dp[0], out double dt) && TryDouble(dp[1], out double du))
                { m.DiskTotalGB = dt; m.DiskUsedGB = du; }

                // Uptime
                m.Uptime = (await tUp).Replace("up ", "").Trim();

                // Load
                var lp = (await tLoad).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (lp.Length >= 3)
                {
                    if (TryDouble(lp[0], out double l1)) m.Load1m = l1;
                    if (TryDouble(lp[1], out double l5)) m.Load5m = l5;
                    if (TryDouble(lp[2], out double l15)) m.Load15m = l15;
                }

                // Connections
                if (int.TryParse((await tConn).Trim(), out int conn)) m.ActiveConnections = conn;

                // Temperature
                var ts = (await tTemp).Trim();
                if (!string.IsNullOrEmpty(ts) && TryDouble(ts, out double temp) && temp > 0)
                { m.CpuTempC = temp; m.HasCpuTemp = true; }

                // Extra info
                m.KernelVersion = (await tKern).Trim();
                if (int.TryParse((await tProc).Trim(), out int procs)) m.ProcessCount = procs;

                m.LastUpdated = DateTime.Now;
                client.Disconnect();
            }
            catch (Exception ex)
            {
                m.IsOnline = false;
                string msg = ex.Message;
                m.ErrorMessage = msg.Length > 90 ? msg[..90] + "…" : msg;
            }

            return m;
        }

        private static Task<string> RunAsync(SshClient client, string cmd) =>
            Task.Run(() =>
            {
                try
                {
                    using var c = client.CreateCommand(cmd);
                    c.CommandTimeout = TimeSpan.FromSeconds(CmdTimeoutSec);
                    return c.Execute()?.Trim() ?? string.Empty;
                }
                catch { return string.Empty; }
            });

        private static bool TryDouble(string s, out double v) =>
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }
}
