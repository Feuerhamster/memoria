using System.ComponentModel.DataAnnotations;
using Memoria.Models.Database;

namespace Memoria.Models.Request;

public class FileUploadRequest
{
    [Required]
    public required IFormFile File { get; set; }

    /// <summary>
    /// The post this file gets attached to. The file's space is derived from that post — upload
    /// the post first, then attach files to it via its id.
    /// </summary>
    [Required]
    public Guid PostId { get; set; }

    public RessourceAccessPolicy? AccessPolicy { get; set; }
}

public class FileUpdateRequest : IDataUpdateObject<FileMetadata>
{
    public string? Name { get; set; }

    /// <summary>
    /// A file's space always follows the post it's attached to — it cannot be moved to another
    /// space independently. To move a file, move (or re-attach it to a post in) the target space.
    /// </summary>
    public RessourceAccessPolicy? AccessPolicy { get; set; }

    public void Apply(FileMetadata file)
    {
        if (this.Name != null)
        {
            file.FileName = this.Name;
        }

        if (this.AccessPolicy != null)
        {
            file.AccessPolicy = this.AccessPolicy.Value;
        }

        file.ModifiedAt = DateTime.UtcNow;
    }
}