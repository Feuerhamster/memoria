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

    /// <summary>
    /// Whether this ressource may be surfaced to AI context providers (e.g. MCP).
    /// Independent of <see cref="AccessPolicy"/>, since human access rules don't automatically
    /// imply it's safe or desired to hand the content to an AI/agent.
    /// </summary>
    public bool ContextAvailability { get; }
}