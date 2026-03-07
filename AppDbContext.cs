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

    public DbSet<CalendarEventCache> CalendarEventCache { get; set; }
    public DbSet<ContactCache> ContactCache { get; set; }

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

        this.CreatePostModel(modelBuilder);
        this.CreateRadicaleCacheModels(modelBuilder);
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

    private void CreatePostModel(ModelBuilder modelBuilder)
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
                .HasOne<FileMetadata>(e => e.File)
                .WithMany();

            // CalendarEventId is a plain Guid? column — no FK constraint to Radicale cache
            entity.Property(e => e.CalendarEventId);
        });
    }

    private void CreateRadicaleCacheModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarEventCache>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Space>()
                .WithMany()
                .HasForeignKey(e => e.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SpaceId);
        });

        modelBuilder.Entity<ContactCache>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Space>()
                .WithMany()
                .HasForeignKey(e => e.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SpaceId);
        });
    }
}
