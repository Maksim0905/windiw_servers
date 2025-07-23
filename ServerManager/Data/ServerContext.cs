using Microsoft.EntityFrameworkCore;
using ServerManager.Models;

namespace ServerManager.Data
{
    public class ServerContext : DbContext
    {
        public ServerContext(DbContextOptions<ServerContext> options) : base(options)
        {
        }
        
        public DbSet<ServerInfo> Servers { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ServerInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Address).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Username).HasMaxLength(50);
                entity.Property(e => e.Password).HasMaxLength(500);
                entity.Property(e => e.PrivateKeyPath).HasMaxLength(500);
                entity.Property(e => e.Tags).HasMaxLength(1000);
                entity.Property(e => e.CpuUsage).HasMaxLength(50);
                entity.Property(e => e.MemoryUsage).HasMaxLength(50);
                entity.Property(e => e.DiskUsage).HasMaxLength(50);
                entity.Property(e => e.Uptime).HasMaxLength(100);
                entity.Property(e => e.OsInfo).HasMaxLength(200);
                
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Address);
                entity.HasIndex(e => e.Status);
            });
            
            base.OnModelCreating(modelBuilder);
        }
    }
}