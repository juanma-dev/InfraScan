using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfraScan.Models;
using Renci.SshNet;

namespace InfraScan.Services
{
    public class SshService : IDisposable
    {
        private SshClient? _client;
        private readonly ServerConnection _server;
        private readonly string _password;

        public event Action<string>? OnLog;

        public SshService(ServerConnection server)
        {
            _server = server;
            _password = CredentialService.Decrypt(server.EncryptedPassword);
        }

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var connectionInfo = new ConnectionInfo(
                        _server.Host,
                        _server.Port,
                        _server.Username,
                        new PasswordAuthenticationMethod(_server.Username, _password)
                    )
                    {
                        Timeout = TimeSpan.FromSeconds(30)
                    };

                    _client = new SshClient(connectionInfo);
                    _client.Connect();
                    Log($"✅ Conectado a {_server.Host}:{_server.Port} como {_server.Username}");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"❌ Error de conexión: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<string> ExecuteCommandAsync(string command, int timeoutSeconds = 60)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SSH no conectado");

            return await Task.Run(() =>
            {
                try
                {
                    Log($"$ {command}");
                    using var cmd = _client.CreateCommand(command);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds);
                    string result = cmd.Execute();
                    string error = cmd.Error;

                    if (!string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(result))
                    {
                        Log($"⚠️ {error.Trim()}");
                        return error.Trim();
                    }

                    string output = result.Trim();
                    if (output.Length > 200)
                        Log($"  → {output[..200]}...");
                    else
                        Log($"  → {output}");

                    return output;
                }
                catch (Exception ex)
                {
                    Log($"❌ Error ejecutando comando: {ex.Message}");
                    return $"ERROR: {ex.Message}";
                }
            });
        }

        public async Task<Dictionary<string, string>> ExecuteAllCommandsAsync(
            List<CommandConfig> commands,
            IProgress<(int current, int total, string name)>? progress = null,
            CancellationToken ct = default)
        {
            var results = new Dictionary<string, string>();
            int total = commands.Count;

            for (int i = 0; i < commands.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var cmd = commands[i];

                progress?.Report((i + 1, total, cmd.Name));
                Log($"\n📋 [{i + 1}/{total}] {cmd.Name}");

                string output = await ExecuteCommandAsync(cmd.Command);
                results[cmd.Name] = output;
            }

            return results;
        }

        public void Disconnect()
        {
            if (_client?.IsConnected == true)
            {
                _client.Disconnect();
                Log("🔌 Desconectado del servidor");
            }
        }

        public void Dispose()
        {
            Disconnect();
            _client?.Dispose();
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
}
