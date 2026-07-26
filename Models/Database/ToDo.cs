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
    public Guid Id { get; }
    public Guid OwnerUserId { get; }
    public Guid? SpaceId { get; }
    public RessourceAccessPolicy AccessPolicy { get; }
    
    public bool ContextAvailability { get; }
    
    public DateTime CreatedAt;
    public DateTime ModifiedAt;

    public Guid PostId;
    
    public string Title { get; set; }
    public string? Description { get; set; }
    
    public EToTicketStatus Status { get; set; }
    public ETicketPriority Priority { get; set; }
    
    public List<SubTask> SubTasks { get; set; }
    
    public DateTime DueDate { get; set; }
    
    public List<User> Assignees { get; set; }
}

public class SubTask
{
    public string Name { get; set; }
    public bool Checked { get; set; }
}