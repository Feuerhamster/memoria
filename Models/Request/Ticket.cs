using System.ComponentModel.DataAnnotations;
using Memoria.Models.Database;

namespace Memoria.Models.Request;

public class CreateTicketRequest
{
    /// <summary>
    /// The post this ticket is created for — must already exist. Same principle as file uploads:
    /// create the post first, then create the ticket referencing it.
    /// </summary>
    [Required]
    public Guid PostId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public EToTicketStatus Status { get; set; } = EToTicketStatus.Open;
    public ETicketPriority Priority { get; set; } = ETicketPriority.Normal;

    public DateTime? DueDate { get; set; }

    public List<Guid>? AssigneeIds { get; set; }
}

public class UpdateTicketRequest : IDataUpdateObject<Ticket>
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public EToTicketStatus? Status { get; set; }
    public ETicketPriority? Priority { get; set; }
    public DateTime? DueDate { get; set; }

    public void Apply(Ticket ticket)
    {
        if (this.Title != null)
        {
            ticket.Title = this.Title;
        }

        if (this.Description != null)
        {
            ticket.Description = this.Description;
        }

        if (this.Status != null)
        {
            ticket.Status = this.Status.Value;
        }

        if (this.Priority != null)
        {
            ticket.Priority = this.Priority.Value;
        }

        if (this.DueDate != null)
        {
            ticket.DueDate = this.DueDate;
        }

        ticket.ModifiedAt = DateTime.UtcNow;
    }
}
