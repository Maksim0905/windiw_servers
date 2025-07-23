using Microsoft.EntityFrameworkCore;
using ServerManager.Data;
using ServerManager.Models;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text.Json;

namespace ServerManager.Services
{
    public class WindowsServerService
    {
        private readonly ServerContext _context;
        private readonly ILogger<WindowsServerService> _logger;

        public WindowsServerService(ServerContext context, ILogger<WindowsServerService> logger)
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

                // Для Windows серверов получаем системную информацию через WMI
                if (!string.IsNullOrEmpty(server.Username) && !string.IsNullOrEmpty(server.Password))
                {
                    await GetWindowsServerSystemInfoAsync(server);
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

        private async Task GetWindowsServerSystemInfoAsync(ServerInfo server)
        {
            try
            {
                var connectionOptions = new ConnectionOptions
                {
                    Username = server.Username,
                    Password = server.Password,
                    Impersonation = ImpersonationLevel.Impersonate,
                    Authentication = AuthenticationLevel.PacketPrivacy,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var managementScope = new ManagementScope($"\\\\{server.Address}\\root\\cimv2", connectionOptions);
                await Task.Run(() => managementScope.Connect());

                if (managementScope.IsConnected)
                {
                    // Получаем информацию о CPU
                    await GetCpuInfoAsync(managementScope, server);
                    
                    // Получаем информацию о памяти
                    await GetMemoryInfoAsync(managementScope, server);
                    
                    // Получаем информацию о дисках
                    await GetDiskInfoAsync(managementScope, server);
                    
                    // Получаем время работы
                    await GetUptimeInfoAsync(managementScope, server);
                    
                    // Получаем информацию об ОС
                    await GetOsInfoAsync(managementScope, server);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get WMI info for server {ServerId}", server.Id);
            }
        }

        private async Task GetCpuInfoAsync(ManagementScope scope, ServerInfo server)
        {
            try
            {
                var query = new ObjectQuery("SELECT LoadPercentage FROM Win32_Processor");
                using var searcher = new ManagementObjectSearcher(scope, query);
                
                await Task.Run(() =>
                {
                    using var collection = searcher.Get();
                    var cpuUsages = new List<double>();
                    
                    foreach (ManagementObject obj in collection)
                    {
                        if (obj["LoadPercentage"] != null)
                        {
                            cpuUsages.Add(Convert.ToDouble(obj["LoadPercentage"]));
                        }
                        obj?.Dispose();
                    }
                    
                    if (cpuUsages.Any())
                    {
                        server.CpuUsage = $"{cpuUsages.Average():F1}%";
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get CPU info for server {ServerId}", server.Id);
            }
        }

        private async Task GetMemoryInfoAsync(ManagementScope scope, ServerInfo server)
        {
            try
            {
                var query = new ObjectQuery("SELECT TotalVisibleMemorySize, AvailablePhysicalMemory FROM Win32_OperatingSystem");
                using var searcher = new ManagementObjectSearcher(scope, query);
                
                await Task.Run(() =>
                {
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        if (obj["TotalVisibleMemorySize"] != null && obj["AvailablePhysicalMemory"] != null)
                        {
                            var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                            var available = Convert.ToDouble(obj["AvailablePhysicalMemory"]);
                            var used = total - available;
                            var usagePercent = (used / total) * 100;
                            
                            server.MemoryUsage = $"{usagePercent:F1}%";
                        }
                        obj?.Dispose();
                        break;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get memory info for server {ServerId}", server.Id);
            }
        }

        private async Task GetDiskInfoAsync(ManagementScope scope, ServerInfo server)
        {
            try
            {
                var query = new ObjectQuery("SELECT Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3 AND DeviceID = 'C:'");
                using var searcher = new ManagementObjectSearcher(scope, query);
                
                await Task.Run(() =>
                {
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        if (obj["Size"] != null && obj["FreeSpace"] != null)
                        {
                            var total = Convert.ToDouble(obj["Size"]);
                            var free = Convert.ToDouble(obj["FreeSpace"]);
                            var used = total - free;
                            var usagePercent = (used / total) * 100;
                            
                            server.DiskUsage = $"{usagePercent:F1}%";
                        }
                        obj?.Dispose();
                        break;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get disk info for server {ServerId}", server.Id);
            }
        }

        private async Task GetUptimeInfoAsync(ManagementScope scope, ServerInfo server)
        {
            try
            {
                var query = new ObjectQuery("SELECT LastBootUpTime FROM Win32_OperatingSystem");
                using var searcher = new ManagementObjectSearcher(scope, query);
                
                await Task.Run(() =>
                {
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        if (obj["LastBootUpTime"] != null)
                        {
                            var bootTime = ManagementDateTimeConverter.ToDateTime(obj["LastBootUpTime"].ToString());
                            var uptime = DateTime.Now - bootTime;
                            
                            server.Uptime = $"{uptime.Days}д {uptime.Hours}ч {uptime.Minutes}м";
                        }
                        obj?.Dispose();
                        break;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get uptime info for server {ServerId}", server.Id);
            }
        }

        private async Task GetOsInfoAsync(ManagementScope scope, ServerInfo server)
        {
            try
            {
                var query = new ObjectQuery("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem");
                using var searcher = new ManagementObjectSearcher(scope, query);
                
                await Task.Run(() =>
                {
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        var caption = obj["Caption"]?.ToString() ?? "";
                        var version = obj["Version"]?.ToString() ?? "";
                        var architecture = obj["OSArchitecture"]?.ToString() ?? "";
                        
                        server.OsInfo = $"{caption} {version} ({architecture})";
                        obj?.Dispose();
                        break;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get OS info for server {ServerId}", server.Id);
            }
        }

        public async Task<string> ExecuteCommandAsync(int serverId, string command)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) throw new ArgumentException("Server not found");

            if (string.IsNullOrEmpty(server.Username) || string.IsNullOrEmpty(server.Password))
                throw new InvalidOperationException("Server credentials not configured");

            try
            {
                return await ExecuteRemoteCommandAsync(server, command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command on server {ServerId}", serverId);
                throw;
            }
        }

        private async Task<string> ExecuteRemoteCommandAsync(ServerInfo server, string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Invoke-Command -ComputerName {server.Address} -Credential (New-Object System.Management.Automation.PSCredential('{server.Username}', (ConvertTo-SecureString '{server.Password}' -AsPlainText -Force))) -ScriptBlock {{ {command} }}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                {
                    return $"Error: {error}";
                }

                return string.IsNullOrEmpty(output) ? "Command executed successfully (no output)" : output;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to execute remote command: {ex.Message}", ex);
            }
        }

        public async Task<List<WindowsServiceInfo>> GetWindowsServicesAsync(int serverId)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) throw new ArgumentException("Server not found");

            if (string.IsNullOrEmpty(server.Username) || string.IsNullOrEmpty(server.Password))
                throw new InvalidOperationException("Server credentials not configured");

            try
            {
                var services = new List<WindowsServiceInfo>();
                
                var connectionOptions = new ConnectionOptions
                {
                    Username = server.Username,
                    Password = server.Password,
                    Impersonation = ImpersonationLevel.Impersonate,
                    Authentication = AuthenticationLevel.PacketPrivacy,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var managementScope = new ManagementScope($"\\\\{server.Address}\\root\\cimv2", connectionOptions);
                await Task.Run(() => managementScope.Connect());

                if (managementScope.IsConnected)
                {
                    var query = new ObjectQuery("SELECT Name, DisplayName, State, StartMode, ProcessId FROM Win32_Service");
                    using var searcher = new ManagementObjectSearcher(managementScope, query);
                    
                    await Task.Run(() =>
                    {
                        using var collection = searcher.Get();
                        foreach (ManagementObject obj in collection)
                        {
                            services.Add(new WindowsServiceInfo
                            {
                                Name = obj["Name"]?.ToString() ?? "",
                                DisplayName = obj["DisplayName"]?.ToString() ?? "",
                                Status = obj["State"]?.ToString() ?? "",
                                StartType = obj["StartMode"]?.ToString() ?? "",
                                ProcessId = Convert.ToInt32(obj["ProcessId"] ?? 0)
                            });
                            obj?.Dispose();
                        }
                    });
                }

                return services;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Windows services for server {ServerId}", serverId);
                throw;
            }
        }

        public async Task<string> ManageWindowsServiceAsync(int serverId, string serviceName, string action)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) throw new ArgumentException("Server not found");

            var command = action.ToLower() switch
            {
                "start" => $"Start-Service -Name '{serviceName}'",
                "stop" => $"Stop-Service -Name '{serviceName}' -Force",
                "restart" => $"Restart-Service -Name '{serviceName}' -Force",
                _ => throw new ArgumentException("Invalid action. Use: start, stop, restart")
            };

            return await ExecuteRemoteCommandAsync(server, command);
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

    public class WindowsServiceInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Status { get; set; } = "";
        public string StartType { get; set; } = "";
        public int ProcessId { get; set; }
    }
}