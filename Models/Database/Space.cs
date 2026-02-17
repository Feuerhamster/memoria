namespace Memoria.Models.Database;

public class Space : IAccessManagedRessource
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
        this.AccessPolicy = RessourceAccessPolicy.Shared;
        this.AllowJoins = true;
    }
    
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? Color { get; set; }
    public Guid? ImageId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? SpaceId => this.Id;

    public DateTime CreatedAt { get; set; }
    
    public List<User> Members { get; set; }
    
    public RessourceAccessPolicy AccessPolicy { get; set; }
    
    public bool AllowJoins { get; set; }
}