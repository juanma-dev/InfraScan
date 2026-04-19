using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InfraScan.Models;
using Newtonsoft.Json;

namespace InfraScan.Services
{
    public class StorageService
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InfraScan"
        );

        private static readonly string ServersFile = Path.Combine(AppDataDir, "servers.json");
        private static readonly string CommandsFile = Path.Combine(AppDataDir, "commands.json");

        public StorageService()
        {
            Directory.CreateDirectory(AppDataDir);
        }

        // === Server operations ===

        public List<ServerConnection> LoadServers()
        {
            if (!File.Exists(ServersFile)) return new List<ServerConnection>();

            try
            {
                string json = File.ReadAllText(ServersFile);
                return JsonConvert.DeserializeObject<List<ServerConnection>>(json) ?? new List<ServerConnection>();
            }
            catch
            {
                return new List<ServerConnection>();
            }
        }

        public void SaveServers(List<ServerConnection> servers)
        {
            string json = JsonConvert.SerializeObject(servers, Formatting.Indented);
            File.WriteAllText(ServersFile, json);
        }

        public void SaveServer(ServerConnection server)
        {
            var servers = LoadServers();
            var existing = servers.FindIndex(s => s.Id == server.Id);
            if (existing >= 0)
                servers[existing] = server;
            else
                servers.Add(server);

            SaveServers(servers);
        }

        public void DeleteServer(string serverId)
        {
            var servers = LoadServers();
            servers.RemoveAll(s => s.Id == serverId);
            SaveServers(servers);
        }

        // === Command config operations ===

        public List<CommandConfig> LoadCommands()
        {
            if (!File.Exists(CommandsFile))
            {
                var defaults = GetDefaultCommands();
                SaveCommands(defaults);
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(CommandsFile);
                var commands = JsonConvert.DeserializeObject<List<CommandConfig>>(json);
                if (commands == null || commands.Count == 0)
                {
                    commands = GetDefaultCommands();
                    SaveCommands(commands);
                }
                return commands;
            }
            catch
            {
                var defaults = GetDefaultCommands();
                SaveCommands(defaults);
                return defaults;
            }
        }

        public void SaveCommands(List<CommandConfig> commands)
        {
            string json = JsonConvert.SerializeObject(commands, Formatting.Indented);
            File.WriteAllText(CommandsFile, json);
        }

        public static List<CommandConfig> GetDefaultCommands()
        {
            return new List<CommandConfig>
            {
                // Table commands
                new("Resumen del sistema",
                    "uptime -p; hostnamectl 2>/dev/null || (hostname; cat /etc/os-release); dmidecode -s system-product-name 2>/dev/null || cat /sys/class/dmi/id/product_name 2>/dev/null || echo 'N/A'",
                    CommandCategory.Table, true, 1),

                new("Rendimiento CPU",
                    "echo \"LOAD:\"; uptime | awk -F'load average:' '{print $2}' | cut -d, -f1 | xargs; echo \"CORES:\"; nproc; echo \"TOP:\"; top -b -n1 | head -5",
                    CommandCategory.Table, true, 2),

                new("Rendimiento memoria",
                    "free -h",
                    CommandCategory.Table, true, 3),

                new("Rendimiento disco",
                    "df -h / | tail -1; df -h /boot 2>/dev/null | tail -1",
                    CommandCategory.Table, true, 4),

                new("Red",
                    "ss -tun state established 2>/dev/null | tail -n +2 | wc -l",
                    CommandCategory.Table, true, 5),

                new("Registros (Logs)",
                    "echo \"SSH_FAILS:\"; journalctl -u sshd --since '24 hours ago' 2>/dev/null | grep -c 'Failed password' || echo '0'; echo \"CRITICAL:\"; journalctl -p 3 --since today --no-pager 2>/dev/null | grep -vE 'sshd|pam|swap' | grep -c . || echo '0'; echo \"ERRORS:\"; journalctl -p 3 --since today --no-pager 2>/dev/null | grep -vE 'sshd|pam|swap' | head -5",
                    CommandCategory.Table, true, 6),

                new("Actualizaciones del sistema",
                    "echo \"UPDATES:\"; (dnf check-update 2>/dev/null | grep -E '^[a-zA-Z0-9]' | wc -l) || (apt list --upgradable 2>/dev/null | grep -c upgradable) || echo '0'; echo \"SECURITY:\"; (dnf updateinfo summary 2>/dev/null | grep -i 'seguridad\\|security' | awk '{print $1}') || echo '0'",
                    CommandCategory.Table, true, 7),

                new("Servicios",
                    "ss -lntu | awk 'NR>1 {split($5, a, \":\"); port=a[length(a)]; if(port~/^[0-9]+$/) print port}' | sort -un | tr '\\n' ' '",
                    CommandCategory.Table, true, 8),

                // Output/Image commands
                new("Top",
                    "top -b -n1 | head -45",
                    CommandCategory.Output, true, 1),

                new("Monitoreo de recursos: almacenamiento, memoria y conexiones",
                    "df -h; echo ''; free -h; echo ''; (netstat -plant 2>/dev/null || ss -lntp) | grep LISTEN",
                    CommandCategory.Output, true, 2),
            };
        }

        // === Report output directory ===

        public static string GetReportsDirectory()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "InfraScan_Reports"
            );
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
