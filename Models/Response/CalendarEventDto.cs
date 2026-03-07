namespace Memoria.Models.Response;

public class CalendarEventDto
{
    public Guid Id { get; set; }
    public Guid SpaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAllDay { get; set; }
    public string? ETag { get; set; }
    public DateTime LastModified { get; set; }
}
