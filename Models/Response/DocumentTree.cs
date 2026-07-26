namespace Memoria.Models.Response;

/// <summary>
/// One node in a space's documentation tree. Deliberately minimal — this is only for building a
/// navigation sidebar; the client fetches the full post separately (<c>GET /posts/{id}</c>) once
/// the user actually navigates to it.
/// </summary>
public class DocumentTreeNode
{
    public Guid Id { get; set; }

    /// <summary>Short excerpt of the post's text, for display in the nav tree — not the full content.</summary>
    public string? Excerpt { get; set; }

    public List<DocumentTreeNode> Children { get; set; } = new();
}
