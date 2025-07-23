using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace windiw_servers.Services
{
    public class ServerApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ServerApiService(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<List<ServerInfo>> GetServersAsync(ServerFilterRequest? filter = null)
        {
            try
            {
                var url = $"{_baseUrl}/api/servers";
                
                if (filter != null)
                {
                    var queryParams = new List<string>();
                    
                    if (!string.IsNullOrEmpty(filter.NameFilter))
                        queryParams.Add($"nameFilter={Uri.EscapeDataString(filter.NameFilter)}");
                    
                    if (!string.IsNullOrEmpty(filter.AddressFilter))
                        queryParams.Add($"addressFilter={Uri.EscapeDataString(filter.AddressFilter)}");
                    
                    if (!string.IsNullOrEmpty(filter.TagFilter))
                        queryParams.Add($"tagFilter={Uri.EscapeDataString(filter.TagFilter)}");
                    
                    if (filter.StatusFilter.HasValue)
                        queryParams.Add($"statusFilter={filter.StatusFilter.Value}");
                    
                    queryParams.Add($"page={filter.Page}");
                    queryParams.Add($"pageSize={filter.PageSize}");
                    
                    if (queryParams.Count > 0)
                        url += "?" + string.Join("&", queryParams);
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var servers = JsonSerializer.Deserialize<List<ServerInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return servers ?? new List<ServerInfo>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting servers: {ex.Message}", ex);
            }
        }

        public async Task<ServerInfo?> GetServerAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/servers/{id}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ServerInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting server {id}: {ex.Message}", ex);
            }
        }

        public async Task<ServerInfo> CreateServerAsync(CreateServerRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/servers", content);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var server = JsonSerializer.Deserialize<ServerInfo>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return server ?? throw new Exception("Failed to deserialize created server");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating server: {ex.Message}", ex);
            }
        }

        public async Task<ServerInfo?> UpdateServerAsync(int id, UpdateServerRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"{_baseUrl}/api/servers/{id}", content);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ServerInfo>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating server {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteServerAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/servers/{id}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return false;
                
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting server {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> CheckServerStatusAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/servers/{id}/check-status", null);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                
                return result?.ContainsKey("success") == true && result["success"].ToString() == "True";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking server status {id}: {ex.Message}", ex);
            }
        }

        public async Task CheckAllServersStatusAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/servers/check-all-status", null);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking all servers status: {ex.Message}", ex);
            }
        }

        public async Task<string> ExecuteCommandAsync(int id, string command)
        {
            try
            {
                var request = new { Command = command };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/servers/{id}/execute", content);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);
                
                return result?["result"]?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error executing command on server {id}: {ex.Message}", ex);
            }
        }

        public async Task<Dictionary<string, int>> GetStatisticsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/servers/statistics");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting statistics: {ex.Message}", ex);
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Модели для API
    public class ServerInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Description { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PrivateKeyPath { get; set; } = "";
        public ServerStatus Status { get; set; } = ServerStatus.Unknown;
        public DateTime LastChecked { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CpuUsage { get; set; }
        public string? MemoryUsage { get; set; }
        public string? DiskUsage { get; set; }
        public string? Uptime { get; set; }
        public string? OsInfo { get; set; }
        public string Tags { get; set; } = "";
        
        // Для совместимости со старой моделью
        public string StatusColor => Status switch
        {
            ServerStatus.Online => "#27ae60",
            ServerStatus.Offline => "#e74c3c",
            ServerStatus.Error => "#f39c12",
            ServerStatus.Checking => "#3498db",
            _ => "#95a5a6"
        };
    }

    public enum ServerStatus
    {
        Unknown,
        Online,
        Offline,
        Error,
        Checking
    }

    public class CreateServerRequest
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Description { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PrivateKeyPath { get; set; } = "";
        public string Tags { get; set; } = "";
    }

    public class UpdateServerRequest
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public int? Port { get; set; }
        public string? Description { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? PrivateKeyPath { get; set; }
        public string? Tags { get; set; }
    }

    public class ServerFilterRequest
    {
        public string? NameFilter { get; set; }
        public string? AddressFilter { get; set; }
        public string? TagFilter { get; set; }
        public ServerStatus? StatusFilter { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}