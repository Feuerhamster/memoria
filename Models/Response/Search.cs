using Memoria.Models.Database;

namespace Memoria.Models.Response;

public enum SearchEntityType
{
    Post,
    Ticket,
    File,
    Space,
    User
}

public class SearchResultResponse
{
    public SearchEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }

    /// <summary>Highlighted excerpt from whichever indexed field matched (title or body), produced by SQLite's <c>snippet()</c>.</summary>
    public string Snippet { get; set; } = string.Empty;

    public PublicEmbeddedUser? Owner { get; set; }
    public EmbeddedSpace? Space { get; set; }
}
