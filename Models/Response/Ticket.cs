using Memoria.Models.Database;

namespace Memoria.Models.Response;

/// <summary>
/// Serializable projection of a <see cref="Ticket"/>. Never serialize <see cref="Ticket"/> directly
/// — its <see cref="Ticket.Post"/> navigation must always be loaded for the derived access fields
/// to work, and serializing that nested <see cref="Post"/> (which references its Ticket back)
/// produces a circular object graph.
/// </summary>
public class TicketResponse
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public PublicEmbeddedUser? Owner { get; set; }
    public EmbeddedSpace? Space { get; set; }
    public RessourceAccessPolicy AccessPolicy { get; set; }
    public bool ContextAvailability { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EToTicketStatus Status { get; set; }
    public ETicketPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public List<SubTask> SubTasks { get; set; } = new();
    public List<PublicEmbeddedUser> Assignees { get; set; } = new();
}
