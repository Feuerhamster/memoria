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