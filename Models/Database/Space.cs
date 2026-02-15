namespace Memoria.Models.Database;

public class Space
{
    public Space()
    {
        
    }

    public Space(string name, string description, Guid ownerUserId)
    {
        this.Name = name;
        this.Description = description;
        this.OwnerUserId = ownerUserId;
        this.CreatedAt = DateTime.UtcNow;
    }
    
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? Color { get; set; }
    public Guid? ImageId { get; set; }
    public Guid OwnerUserId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public List<User> Members { get; set; }
}