namespace Memoria.Models.Request;

public class CreateEventRequest
{
    public string Summary { get; set; } = "";
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAllDay { get; set; }
}

public class UpdateEventRequest
{
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsAllDay { get; set; }
}
