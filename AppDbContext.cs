using Memoria.Models.Config;
using Memoria.Models.Database;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoria;

public class AppDbContext(DbContextOptions<AppDbContext> options, IOptions<DatabaseConfig> config) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserRefreshSession> Sessions { get; set; }
    
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={config.Value.ConnectionString}");
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OidcSub).IsUnique();
            entity.HasIndex(e => e.OidcProvider).IsUnique();
        });
        
        modelBuilder.Entity<UserRefreshSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}