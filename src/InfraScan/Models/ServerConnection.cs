using System;
using Newtonsoft.Json;

namespace InfraScan.Models
{
    public class ServerConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DisplayName { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Base64-encoded DPAPI-encrypted password. Never stored in plain text.
        /// </summary>
        public string EncryptedPassword { get; set; } = string.Empty;

        public string OperatorName { get; set; } = string.Empty;
        public string Contract { get; set; } = string.Empty;
        public string Entity { get; set; } = "LinkTics";
        public string Frequency { get; set; } = "2 veces por semana";

        // Cockpit web (optional)
        public bool HasCockpitWeb { get; set; } = false;
        public string CockpitUrl { get; set; } = string.Empty;
        public string CockpitUsername { get; set; } = string.Empty;
        public string CockpitEncryptedPassword { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastReportDate { get; set; }

        [JsonIgnore]
        public string StatusDisplay => LastReportDate.HasValue
            ? $"Último informe: {LastReportDate.Value:dd/MM/yyyy HH:mm}"
            : "Sin informes generados";
    }
}
