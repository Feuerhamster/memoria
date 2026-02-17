using EFCoreSecondLevelCacheInterceptor;
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
    
    public DbSet<UserAppAccessToken> AppAccessTokens { get; set; }
    
    public DbSet<Space> Spaces { get; set; }
    
    public DbSet<FileMetadata> Files { get; set; }
    
    public DbSet<Post> Posts { get; set; }
    public DbSet<TextNote> TextNotes { get; set; }
    
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={config.Value.ConnectionString}");
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        this.CreateUserManagementModels(modelBuilder);
        
        modelBuilder.Entity<Space>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Members)
                .WithMany();

            entity.HasOne<FileMetadata>()
                .WithMany()
                .HasForeignKey(e => e.ImageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<FileMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne<Space>()
                .WithMany()
                .HasForeignKey(f => f.SpaceId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        this.CreateContentModels(modelBuilder);
    }

    private void CreateUserManagementModels(ModelBuilder modelBuilder)
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

        modelBuilder.Entity<UserAppAccessToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccessToken);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void CreateContentModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired();

            entity.HasOne<Post>()
                .WithMany()
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne<Post>()
                .WithMany()
                .HasForeignKey(e => e.RootParentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity
                .HasMany<FileMetadata>(e => e.Files)
                .WithMany();
        });

        modelBuilder.Entity<TextNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne<Post>()
                .WithOne(t => t.TextNote)
                .HasForeignKey<TextNote>(t => t.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}