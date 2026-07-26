using Memoria.Models.Database;

namespace Memoria.Models.Request;

public class CreatePostRequest
{
    public Guid? SpaceId { get; set; }
    public RessourceAccessPolicy Visibility { get; set; } = RessourceAccessPolicy.Private;

    /// <summary>
    /// May be null/empty — a post can act purely as an organizational element (e.g. the
    /// "created" event for a file or ticket) without any text content of its own.
    /// </summary>
    public string? Text { get; set; }

    public Guid? ParentId { get; set; }
    public Guid? RootParentId { get; set; }

    public bool IsSpaceDocument { get; set; }
}

public class UpdatePostRequest : IDataUpdateObject<Post>
{
    public string? Text { get; set; }
    
    public RessourceAccessPolicy? AccessPolicy {  get; set; }
    
    public bool? IsArchived { get; set; }
    
    public bool? IsSpaceDocument { get; set; }
    
    public void Apply(Post post)
    {
        if (this.Text != null)
        {
            post.Text = this.Text;
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