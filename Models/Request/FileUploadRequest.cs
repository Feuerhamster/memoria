using System.ComponentModel.DataAnnotations;
using Memoria.Models.Database;

namespace Memoria.Models.Request;

public class FileUploadRequest
{
    [Required]
    public required IFormFile File { get; set; }
    
    public Guid? SpaceId { get; set; }
    
    public RessourceAccessPolicy? AccessPolicy { get; set; }
}

public class FileUpdateRequest : IDataUpdateObject<FileMetadata>
{
    public string? Name { get; set; }
    
    public Guid? SpaceId { get; set; }
    
    public RessourceAccessPolicy? AccessPolicy { get; set; }
    
    public void Apply(FileMetadata file)
    {
        if (this.Name != null)
        {
            file.FileName = this.Name;
        }

        if (this.SpaceId != null)
        {
            file.SpaceId = this.SpaceId;
        }

        if (this.AccessPolicy != null)
        {
            file.AccessPolicy = this.AccessPolicy.Value;
        }
        
        file.UploadedAt = DateTime.UtcNow;
    }
}