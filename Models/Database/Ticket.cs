namespace Memoria.Models.Database;

public enum TicketStatus
{
    Open,
    InProgress,
    OnHold,
    Closed
}

public enum ETicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public class TicketSubTask(string name)
{
    public Guid Id { get; set; }
    
    public string Name { get; set; } = name;

    public TicketStatus Status { get; set; } = TicketStatus.Open;
}

public class Ticket : IAccessManagedRessource
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? SpaceId { get; set; }
    public RessourceAccessPolicy AccessPolicy { get; set; }
    
    public string Title { get; set; }
    
    public string Description { get; set; }
    
    public TicketStatus Status { get; set; }
    
    public ETicketPriority Priority { get; set; }
    
    public List<TicketSubTask> SubTasks { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public DateTime? DueDate { get; set; }
    
    public List<User> Assignees { get; set; }
    
    public Post? Post { get; set; }
}