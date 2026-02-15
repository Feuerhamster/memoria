using System.ComponentModel.DataAnnotations;

namespace Memoria.Models.Request;

public class CreatePostRequest
{
    public Guid? SpaceId { get; set; }
    public RessourceAccessPolicy Visibility { get; set; } = RessourceAccessPolicy.Private;
    
    public Guid? ParentId { get; set; }
    public Guid? RootParentId { get; set; }
    
    public List<Guid>? Files { get; set; }
}

public class CreateTextNotePostRequest : CreatePostRequest
{
    [StringLength(1024,  MinimumLength = 2)]
    public string? Title { get; set; }
    
    [MinLength(1)]
    public string Text { get; set; }
    
    public bool InSpaceDocs { get; set; } = false;
}