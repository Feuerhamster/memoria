using Memoria.Models.Database;

namespace Memoria.Models.Response;

public class EmbeddedFile(Guid id, string name, string fileType)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string FileType { get; } = fileType;
}

public class EmbeddedSpace(Guid id, string name)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
}

/// <summary>
/// Serializable projection of a <see cref="Post"/>. Never serialize <see cref="Post"/> directly —
/// since its <see cref="Post.Ticket"/>/<see cref="Ticket.Post"/> back-reference produces a
/// circular object graph.
/// Build from a <see cref="Post"/> that was loaded with <c>Include(p => p.Owner).Include(p => p.Space)
/// .Include(p => p.Files).Include(p => p.Ticket)</c> (use <c>.AsSplitQuery()</c> since Files is a
/// collection alongside several single-reference includes).
/// </summary>
public class PostResponse
{
    public Guid Id { get; }
    public PublicEmbeddedUser Owner { get; }
    public EmbeddedSpace? Space { get; }
    public RessourceAccessPolicy AccessPolicy { get; }
    public DateTime CreatedAt { get; }
    public DateTime? ModifiedAt { get; }
    public string? Text { get; }
    public Guid? ParentId { get; }
    public Guid? RootParentId { get; }
    public bool IsArchived { get; }
    public bool IsSpaceDocument { get; }
    public List<EmbeddedFile> Files { get; }
    public Guid? TicketId { get; }

    public PostResponse(Post post)
    {
        this.Id = post.Id;
        this.Owner = new PublicEmbeddedUser(post.Owner);
        this.Space = post.Space != null ? new EmbeddedSpace(post.Space.Id, post.Space.Name) : null;
        this.AccessPolicy = post.AccessPolicy;
        this.CreatedAt = post.CreatedAt;
        this.ModifiedAt = post.ModifiedAt;
        this.Text = post.Text;
        this.ParentId = post.ParentId;
        this.RootParentId = post.RootParentId;
        this.IsArchived = post.IsArchived;
        this.IsSpaceDocument = post.IsSpaceDocument;
        this.Files = post.Files.Select(f => new EmbeddedFile(f.Id, f.FileName, f.ContentType)).ToList();
        this.TicketId = post.Ticket?.Id;
    }
}
