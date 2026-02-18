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
        
        this.ParentId = create.ParentId;
        this.RootParentId = create.RootParentId;
    }
    
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? SpaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    
    public string Text { get; set; }
    
    public Guid? ParentId { get; set; }
    public Guid? RootParentId { get; set; }
    
    public FileMetadata? File { get; set; }

    /// <summary>
    /// Optional reference to a CalendarEvent. When set, this post is the "feed entry"
    /// for that event — other posts can reply to this post via ParentId to form a discussion.
    /// </summary>
    public Guid? CalendarEventId { get; set; }
    public CalendarEntry? CalendarEntry { get; set; }
    // public Ticket? Ticket { get; set; }
    
    public RessourceAccessPolicy AccessPolicy {  get; set; }
    
    public bool IsArchived { get; set; }
    
    public bool IsSpaceDocument { get; set; }
}