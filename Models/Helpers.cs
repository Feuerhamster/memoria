namespace Memoria.Models;

public class RessourceOwnerHelper
{
    public Guid? SpaceId { get; set; }
    public Guid UserId { get; set; }
}

public enum RessourceAccessPolicy
{
    Private,
    SpaceMembers,
    GeneralMembers,
    Public
}

public enum RessourceAccessIntention
{
    Read,
    Write
}