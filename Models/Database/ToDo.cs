namespace Memoria.Models.Database;

public enum EToDoStatus
{
    Open,
    InProgress,
    OnHold,
    Done
}

public enum EToDoPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public class ToDo
{
    public Guid Id;
    public DateTime CreatedAt;
    public DateTime ModifiedAt;

    public Guid PostId;
    
    public string Title { get; set; }
    public string? Description { get; set; }
    
    public EToDoStatus Status { get; set; }
    public EToDoPriority Priority { get; set; }
    
    public List<SubTask> SubTasks { get; set; }
    
    public DateTime DueDate { get; set; }
    
    public List<User> Assignees { get; set; }
}

public class SubTask
{
    public string Name { get; set; }
    public bool Checked { get; set; }
}