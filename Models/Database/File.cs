namespace Memoria.Models.Database;



public class FileMetadata : IAccessManagedRessource
{
    public Guid Id { get; set; }
    
    public Guid OwnerUserId { get; set; }
    public Guid? SpaceId { get; set; }
    
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    
    public RessourceAccessPolicy  AccessPolicy { get; set; }
}