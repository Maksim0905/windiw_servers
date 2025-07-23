using System.ComponentModel.DataAnnotations;

namespace ServerManager.Models
{
    public class ServerInfo
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";
        
        [Required]
        [StringLength(255)]
        public string Address { get; set; } = "";
        
        [Range(1, 65535)]
        public int Port { get; set; } = 3389; // RDP порт для Windows
        
        [StringLength(500)]
        public string Description { get; set; } = "";
        
        [StringLength(50)]
        public string Username { get; set; } = "";
        
        [StringLength(500)]
        public string Password { get; set; } = ""; // В реальном проекте лучше использовать ключи
        
        [StringLength(500)]
        public string PrivateKeyPath { get; set; } = "";
        
        public ServerStatus Status { get; set; } = ServerStatus.Unknown;
        
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Системная информация
        public string? CpuUsage { get; set; }
        public string? MemoryUsage { get; set; }
        public string? DiskUsage { get; set; }
        public string? Uptime { get; set; }
        public string? OsInfo { get; set; }
        
        // Теги для фильтрации
        public string Tags { get; set; } = "";
        
        public List<string> GetTags() => Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();
        
        public void SetTags(IEnumerable<string> tags) => Tags = string.Join(",", tags);
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
        [Required]
        public string Name { get; set; } = "";
        
        [Required]
        public string Address { get; set; } = "";
        
        public int Port { get; set; } = 3389;
        
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