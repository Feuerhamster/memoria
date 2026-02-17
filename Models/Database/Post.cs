using Memoria.Models.Request;

namespace Memoria.Models.Database;

public class Post : IAccessManagedRessource
{
    public Post()
    {
        
    }

    public Post(Guid ownerUserId, CreateTextNotePostRequest create)
    {
        this.OwnerUserId = ownerUserId;
        this.SpaceId = create.SpaceId;
        this.AccessPolicy = create.Visibility;
        
        this.CreatedAt = DateTime.UtcNow;
        
        this.IsArchived = false;
        
        this.ParentId = create.ParentId;
        this.RootParentId = create.RootParentId;

        this.TextNote = new TextNote()
        {
            Title = create.Title,
            Text = create.Text,
            IsInSpaceDocs = create.InSpaceDocs,
            PostId = this.Id
        };
    }
    
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? SpaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    
    public Guid? ParentId { get; set; }
    public Guid? RootParentId { get; set; }
    
    public TextNote? TextNote { get; set; }
    public List<FileMetadata>? Files { get; set; }
    
    public RessourceAccessPolicy AccessPolicy {  get; set; }
    
    public bool IsArchived { get; set; }
}

public class TextNote
{
    public Guid Id { get; set; }
    public string? Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    
    public Guid PostId { get; set; }
    
    public bool IsInSpaceDocs { get; set; }
}