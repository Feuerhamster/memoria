using Memoria.Models;

namespace Memoria.Models.Database;

/// <summary>
/// Slim cache table storing only access-control metadata for contacts in Radicale.
/// The actual .vcf content lives in Radicale; this table exists solely for access-policy checks.
/// </summary>
public class ContactCache : IAccessManagedRessource
{
    /// <summary>Matches the Radicale filename (without .vcf extension).</summary>
    public Guid Id { get; set; }
    public Guid SpaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public RessourceAccessPolicy AccessPolicy { get; set; } = RessourceAccessPolicy.Members;
    public string? ETag { get; set; }
    public DateTime LastModified { get; set; }

    Guid? IAccessManagedRessource.SpaceId => SpaceId;
}
