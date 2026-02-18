using Memoria.Models.Database;

namespace Memoria.Models.WebDav;

public class CaldavEventMetadata(Guid id, int sequence, DateTime lastModified)
{
    public CaldavEventMetadata(CalendarEntry calEvent) : this(calEvent.Id, calEvent.Sequence, calEvent.LastModified)
    {
    }
    
    public Guid Id { get; } = id;
    public int Sequence { get; } = sequence;
    public DateTime LastModified { get; } = lastModified;
}