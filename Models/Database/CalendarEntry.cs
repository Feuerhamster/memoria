using Ical.Net;

namespace Memoria.Models.Database;

public class CalendarEntry : IAccessManagedRessource
{
    public Guid Id { get; set; }
    
    public Guid OwnerUserId { get; set; }
    public Guid SpaceId { get; set; }
    public RessourceAccessPolicy AccessPolicy { get; set; } = RessourceAccessPolicy.Members;

    // Explicit interface implementation for SpaceId (nullable in interface, non-nullable here)
    Guid? IAccessManagedRessource.SpaceId => SpaceId;

    // --- Basic properties ---
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }

    // --- Time ---
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAllDay { get; set; }
    
    // --- Recurrent Rule --- 

    public FrequencyType? RecurrenceFrequency  { get; set; }
    public int? RecurrenceInterval { get; set; }
    public int? RecurrenceCount { get; set; }
    public DateTime? RecurrenceUntil { get; set; }
    
    // --- Metadata ---
    /// <summary>iCal SEQUENCE number — incremented on every update.</summary>
    public int Sequence { get; set; } = 0;

    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
}
