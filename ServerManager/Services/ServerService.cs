using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using ServerManager.Data;
using ServerManager.Models;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace ServerManager.Services
{
    public class ServerService
    {
        private readonly ServerContext _context;
        private readonly ILogger<ServerService> _logger;

        public ServerService(ServerContext context, ILogger<ServerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<ServerInfo>> GetServersAsync(ServerFilterRequest? filter = null)
        {
            var query = _context.Servers.AsQueryable();

            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.NameFilter))
                {
                    query = query.Where(s => s.Name.Contains(filter.NameFilter));
                }

                if (!string.IsNullOrEmpty(filter.AddressFilter))
                {
                    query = query.Where(s => s.Address.Contains(filter.AddressFilter));
                }

                if (!string.IsNullOrEmpty(filter.TagFilter))
                {
                    query = query.Where(s => s.Tags.Contains(filter.TagFilter));
                }

                if (filter.StatusFilter.HasValue)
                {
                    query = query.Where(s => s.Status == filter.StatusFilter.Value);
                }

                query = query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize);
            }

            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<ServerInfo?> GetServerAsync(int id)
        {
            return await _context.Servers.FindAsync(id);
        }

        public async Task<ServerInfo> CreateServerAsync(CreateServerRequest request)
        {
            var server = new ServerInfo
            {
                Name = request.Name,
                Address = request.Address,
                Port = request.Port,
                Description = request.Description,
                Username = request.Username,
                Password = request.Password,
                PrivateKeyPath = request.PrivateKeyPath,
                Tags = request.Tags,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Servers.Add(server);
            await _context.SaveChangesAsync();

            // Проверяем статус сервера асинхронно
            _ = Task.Run(() => CheckServerStatusAsync(server.Id));

            return server;
        }

        public async Task<ServerInfo?> UpdateServerAsync(int id, UpdateServerRequest request)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null) return null;

            if (request.Name != null) server.Name = request.Name;
            if (request.Address != null) server.Address = request.Address;
            if (request.Port.HasValue) server.Port = request.Port.Value;
            if (request.Description != null) server.Description = request.Description;
            if (request.Username != null) server.Username = request.Username;
            if (request.Password != null) server.Password = request.Password;
            if (request.PrivateKeyPath != null) server.PrivateKeyPath = request.PrivateKeyPath;
            if (request.Tags != null) server.Tags = request.Tags;

            server.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return server;
        }

        public async Task<bool> DeleteServerAsync(int id)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null) return false;

            _context.Servers.Remove(server);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CheckServerStatusAsync(int serverId)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) return false;

            server.Status = ServerStatus.Checking;
            server.LastChecked = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                // Проверяем пинг
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(server.Address, 5000);
                
                if (reply.Status != IPStatus.Success)
                {
                    server.Status = ServerStatus.Offline;
                    await _context.SaveChangesAsync();
                    return false;
                }

                // Проверяем SSH подключение и получаем системную информацию
                if (!string.IsNullOrEmpty(server.Username))
                {
                    await GetServerSystemInfoAsync(server);
                }

                server.Status = ServerStatus.Online;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking server {ServerId}", serverId);
                server.Status = ServerStatus.Error;
                await _context.SaveChangesAsync();
                return false;
            }
        }

        private async Task GetServerSystemInfoAsync(ServerInfo server)
        {
            try
            {
                ConnectionInfo connectionInfo;

                if (!string.IsNullOrEmpty(server.PrivateKeyPath) && File.Exists(server.PrivateKeyPath))
                {
                    var keyFile = new PrivateKeyFile(server.PrivateKeyPath);
                    connectionInfo = new ConnectionInfo(server.Address, server.Port, server.Username, new PrivateKeyAuthenticationMethod(server.Username, keyFile));
                }
                else if (!string.IsNullOrEmpty(server.Password))
                {
                    connectionInfo = new ConnectionInfo(server.Address, server.Port, server.Username, new PasswordAuthenticationMethod(server.Username, server.Password));
                }
                else
                {
                    return;
                }

                using var client = new SshClient(connectionInfo);
                await Task.Run(() => client.Connect());

                if (client.IsConnected)
                {
                    // Получаем информацию о CPU
                    var cpuCommand = client.CreateCommand("top -bn1 | grep \"Cpu(s)\" | sed \"s/.*, *\\([0-9.]*\\)%* id.*/\\1/\" | awk '{print 100 - $1\"%\"}'");
                    var cpuResult = cpuCommand.Execute();
                    if (!string.IsNullOrEmpty(cpuResult.Trim()))
                    {
                        server.CpuUsage = cpuResult.Trim();
                    }

                    // Получаем информацию о памяти
                    var memCommand = client.CreateCommand("free | grep Mem | awk '{printf \"%.1f%%\", $3/$2 * 100.0}'");
                    var memResult = memCommand.Execute();
                    if (!string.IsNullOrEmpty(memResult.Trim()))
                    {
                        server.MemoryUsage = memResult.Trim();
                    }

                    // Получаем информацию о диске
                    var diskCommand = client.CreateCommand("df -h / | awk 'NR==2{print $5}'");
                    var diskResult = diskCommand.Execute();
                    if (!string.IsNullOrEmpty(diskResult.Trim()))
                    {
                        server.DiskUsage = diskResult.Trim();
                    }

                    // Получаем время работы
                    var uptimeCommand = client.CreateCommand("uptime -p");
                    var uptimeResult = uptimeCommand.Execute();
                    if (!string.IsNullOrEmpty(uptimeResult.Trim()))
                    {
                        server.Uptime = uptimeResult.Trim();
                    }

                    // Получаем информацию об ОС
                    var osCommand = client.CreateCommand("lsb_release -d 2>/dev/null | cut -f2 || cat /etc/os-release | grep PRETTY_NAME | cut -d'\"' -f2");
                    var osResult = osCommand.Execute();
                    if (!string.IsNullOrEmpty(osResult.Trim()))
                    {
                        server.OsInfo = osResult.Trim();
                    }

                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get system info for server {ServerId}", server.Id);
            }
        }

        public async Task<string> ExecuteCommandAsync(int serverId, string command)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) throw new ArgumentException("Server not found");

            if (string.IsNullOrEmpty(server.Username))
                throw new InvalidOperationException("Server credentials not configured");

            try
            {
                ConnectionInfo connectionInfo;

                if (!string.IsNullOrEmpty(server.PrivateKeyPath) && File.Exists(server.PrivateKeyPath))
                {
                    var keyFile = new PrivateKeyFile(server.PrivateKeyPath);
                    connectionInfo = new ConnectionInfo(server.Address, server.Port, server.Username, new PrivateKeyAuthenticationMethod(server.Username, keyFile));
                }
                else if (!string.IsNullOrEmpty(server.Password))
                {
                    connectionInfo = new ConnectionInfo(server.Address, server.Port, server.Username, new PasswordAuthenticationMethod(server.Username, server.Password));
                }
                else
                {
                    throw new InvalidOperationException("No valid authentication method configured");
                }

                using var client = new SshClient(connectionInfo);
                await Task.Run(() => client.Connect());

                if (client.IsConnected)
                {
                    var cmd = client.CreateCommand(command);
                    var result = cmd.Execute();
                    client.Disconnect();
                    
                    return result;
                }

                throw new InvalidOperationException("Could not connect to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command on server {ServerId}", serverId);
                throw;
            }
        }

        public async Task CheckAllServersStatusAsync()
        {
            var servers = await _context.Servers.ToListAsync();
            var tasks = servers.Select(s => CheckServerStatusAsync(s.Id));
            await Task.WhenAll(tasks);
        }

        public async Task<Dictionary<string, int>> GetServerStatisticsAsync()
        {
            var stats = new Dictionary<string, int>();
            
            stats["Total"] = await _context.Servers.CountAsync();
            stats["Online"] = await _context.Servers.CountAsync(s => s.Status == ServerStatus.Online);
            stats["Offline"] = await _context.Servers.CountAsync(s => s.Status == ServerStatus.Offline);
            stats["Error"] = await _context.Servers.CountAsync(s => s.Status == ServerStatus.Error);
            stats["Unknown"] = await _context.Servers.CountAsync(s => s.Status == ServerStatus.Unknown);

            return stats;
        }
    }
}