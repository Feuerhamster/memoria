namespace Memoria.Models.Database;

public enum EToTicketStatus
{
    Open,
    InProgress,
    OnHold,
    Done
}

public enum ETicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public class Ticket : IAccessManagedRessource
{
    public Guid Id { get; set; }

    /// <summary>
    /// The post that acts as this ticket's "created" event in the timeline and its administrative
    /// instance. A ticket cannot exist detached from its post, so ownership/space/access are
    /// derived from it rather than duplicated — there is only one source of truth.
    /// Must be loaded (e.g. via <c>Include(t => t.Post)</c>) before these are accessed.
    /// </summary>
    public Guid PostId { get; set; }
    public Post Post { get; set; }

    public Guid OwnerUserId => Post.OwnerUserId;
    public Guid? SpaceId => Post.SpaceId;
    public RessourceAccessPolicy AccessPolicy => Post.AccessPolicy;
    public bool ContextAvailability => Post.ContextAvailability;

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public string Title { get; set; }
    public string? Description { get; set; }

    public EToTicketStatus Status { get; set; }
    public ETicketPriority Priority { get; set; }

    public List<SubTask> SubTasks { get; set; } = new();

    public DateTime? DueDate { get; set; }

    public List<User> Assignees { get; set; } = new();
}

public class SubTask
{
    public string Name { get; set; }
    public bool Checked { get; set; }
}
