namespace Memoria.Models;

public class RessourceOwnerHelper
{
    public Guid? SpaceId { get; set; }
    public Guid UserId { get; set; }
}

public enum RessourceAccessPolicy
{
    /// <summary>
    /// Completely open for everyone, including non-logged in users
    /// </summary>
    Public,
    
    /// <summary>
    /// Available to all logged-in users
    /// </summary>
    Shared,
    
    /// <summary>
    /// Available only to space members
    /// </summary>
    Members,
    
    /// <summary>
    /// Only available for myself
    /// </summary>
    Private,
}

public enum AccessIntent
{
    Read,
    Write
}

public interface IDataUpdateObject<in TElement> {
    public void Apply(TElement element);
}

public interface IAccessManagedRessource
{
    public Guid Id { get; }
    public Guid OwnerUserId { get; }
    public Guid? SpaceId { get; }
    public RessourceAccessPolicy  AccessPolicy { get; }
}