namespace Memoria.Models.Response;

public class ContactDto
{
    public Guid Id { get; set; }
    public Guid SpaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string FormattedName { get; set; } = "";
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Notes { get; set; }
    public string? ETag { get; set; }
    public DateTime LastModified { get; set; }
}
