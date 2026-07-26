using Memoria.Models.Request;

namespace Memoria.Models.Database;

public class Post : IAccessManagedRessource
{
    public Post()
    {
        
    }

    public Post(Guid ownerUserId, CreatePostRequest create)
    {
        this.OwnerUserId = ownerUserId;
        this.SpaceId = create.SpaceId;
        this.AccessPolicy = create.Visibility;

        this.CreatedAt = DateTime.UtcNow;

        this.IsArchived = false;

        this.Text = create.Text;

        this.ParentId = create.ParentId;
        this.RootParentId = create.RootParentId;
    }

    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public User Owner { get; set; }
    public Guid? SpaceId { get; set; }
    public Space? Space { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// A post may have empty text and act purely as an organizational element in the timeline
    /// (e.g. the "created" event for a file or ticket).
    /// </summary>
    public string? Text { get; set; }

    public Guid? ParentId { get; set; }
    public Guid? RootParentId { get; set; }

    public List<FileMetadata> Files { get; set; } = new();

    /// <summary>
    /// The ticket this post is the "created" event for, if any. A post can back at most one ticket.
    /// </summary>
    public Ticket? Ticket { get; set; }

    public RessourceAccessPolicy AccessPolicy {  get; set; }

    public bool ContextAvailability { get; set; } = true;

    public bool IsArchived { get; set; }
    
    public bool IsSpaceDocument { get; set; }
}