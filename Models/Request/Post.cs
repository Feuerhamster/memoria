using System.ComponentModel.DataAnnotations;
using Memoria.Models.Database;

namespace Memoria.Models.Request;

public class CreatePostRequest
{
    public Guid? SpaceId { get; set; }
    public RessourceAccessPolicy Visibility { get; set; } = RessourceAccessPolicy.Private;
 
    [MinLength(1)]
    public string Text { get; set; }
    
    public Guid? ParentId { get; set; }
    public Guid? RootParentId { get; set; }
    
    public Guid? File { get; set; }
}

public class UpdatePostRequest : IDataUpdateObject<Post>
{
    public string? Text { get; set; } = string.Empty;
    
    public RessourceAccessPolicy? AccessPolicy {  get; set; }
    
    public bool? IsArchived { get; set; }
    
    public bool? IsSpaceDocument { get; set; }
    
    public void Apply(Post post)
    {
        if (this.Text != null)
        {
            this.Text = post.Text;
        }

        if (this.AccessPolicy.HasValue)
        {
            post.AccessPolicy = this.AccessPolicy.Value;
        }

        if (this.IsSpaceDocument != null)
        {
            post.IsSpaceDocument = IsSpaceDocument.Value;
        }
        
        if (this.IsArchived != null)
        {
            post.IsArchived = IsArchived.Value;
        }
        
        post.ModifiedAt = DateTime.UtcNow;
    }
}