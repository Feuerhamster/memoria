using Memoria.Models.Database;

namespace Memoria.Models.WebDav;

/// <summary>
/// Information about where to store a file for PUT operations
/// </summary>
public record WebDavPutDestination(
    RessourceAccessPolicy Policy,
    Guid OwnerUserId,
    Guid? SpaceId,
    string FileName
);

public class EntityContext(Guid ownerId, Guid? spaceId, DateTime createdAt, string? fancyName = null)
{
    public Guid OwnerId { get; set; } =  ownerId;
    public Guid? SpaceId { get; set; } = spaceId;
    public bool IsSpaceContext => this.SpaceId.HasValue;
    public DateTime CreatedAt { get; set; } = createdAt;
    public string? FancyName { get; set; } = fancyName;
}

public record ResolvedFile(EntityContext Ctx, RessourceAccessPolicy Policy, FileMetadata? File);