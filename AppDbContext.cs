using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Services.Search;
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

    public DbSet<Ticket> Tickets { get; set; }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={config.Value.ConnectionString}");
    }

    /// <summary>
    /// Keeps the <c>SearchIndexFts</c> full-text index (see <see cref="SearchableTypeRegistry"/>)
    /// in sync with every write to a searchable entity, in the same transaction — no controller
    /// has to remember to update the search index itself.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var searchChanges = this.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new { e.State, Descriptor = SearchableTypeRegistry.ForClrType(e.Entity.GetType()), e.Entity })
            .Where(x => x.Descriptor != null)
            .Select(x => (x.State, Descriptor: x.Descriptor!, Change: x.Descriptor!.Extract(x.Entity)))
            .ToList();

        if (searchChanges.Count == 0)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        await using var transaction = await this.Database.BeginTransactionAsync(cancellationToken);

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var change in searchChanges)
        {
            var entityId = change.Change.Id.ToString();

            await this.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM SearchIndexFts WHERE entity_id = {entityId}", cancellationToken);

            if (change.State != EntityState.Deleted)
            {
                var entityType = change.Descriptor.Type.ToString();
                var title = change.Change.Title;
                var body = change.Change.Body;

                await this.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO SearchIndexFts (entity_type, entity_id, title, body) VALUES ({entityType}, {entityId}, {title}, {body})",
                    cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return result;
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

            entity.HasOne<Post>()
                .WithMany(p => p.Files)
                .HasForeignKey(f => f.PostId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        this.CreatePostModel(modelBuilder);
        this.CreateTicketModel(modelBuilder);
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
            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired();

            entity.HasOne(e => e.Space)
                .WithMany()
                .HasForeignKey(e => e.SpaceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Post>()
                .WithMany()
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Post>()
                .WithMany()
                .HasForeignKey(e => e.RootParentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Supports keyset-paginated timeline queries (WHERE CreatedAt < @cursor ORDER BY CreatedAt DESC)
            // without an expensive OFFSET scan.
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.SpaceId, e.CreatedAt });
        });
    }

    private void CreateTicketModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Ignore(e => e.OwnerUserId);
            entity.Ignore(e => e.SpaceId);
            entity.Ignore(e => e.AccessPolicy);
            entity.Ignore(e => e.ContextAvailability);

            entity.HasIndex(e => e.PostId).IsUnique();

            entity.HasOne(e => e.Post)
                .WithOne(p => p.Ticket)
                .HasForeignKey<Ticket>(e => e.PostId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.OwnsMany(e => e.SubTasks);

            entity.HasMany(e => e.Assignees)
                .WithMany();
        });
    }
}
