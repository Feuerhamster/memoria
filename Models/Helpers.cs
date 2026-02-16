namespace Memoria.Models;

public class RessourceOwnerHelper
{
    public Guid? SpaceId { get; set; }
    public Guid UserId { get; set; }
}

public enum RessourceAccessPolicy
{
    Public,
    Shared,
    Members,
    Private,
}

public enum AccessIntent
{
    Read,
    Write
}