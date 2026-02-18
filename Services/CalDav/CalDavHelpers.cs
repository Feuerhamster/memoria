using System.Security.Cryptography;
using System.Text;
using EFCoreSecondLevelCacheInterceptor;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Memoria.Models.Database;
using Memoria.Models.WebDav;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Services.CalDav;

public static class CalDavHelpers
{
    /// <summary>
    /// Generates a short ETag for a calendar event based on its ID, sequence and last-modified timestamp.
    /// </summary>
    public static string GenerateETag(CaldavEventMetadata meta)
    {
        var raw = $"{meta.Id}-{meta.Sequence}-{meta.LastModified:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Converts a CalDAV event URL segment (the part before <c>.ics</c>) to a stable GUID.
    /// If the segment is already a valid GUID it is returned as-is.
    /// Otherwise a deterministic GUID is derived via SHA-256 so that the same client UID
    /// always maps to the same database record, regardless of whether the client uses
    /// its own UID format (e.g. <c>20260219T120000Z-1234@laptop</c>) or a proper UUID.
    /// </summary>
    public static Guid ParseOrDeriveEventId(string segment)
    {
        if (Guid.TryParse(segment, out var guid)) return guid;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(segment));
        return new Guid(hash[..16]);
    }

    /// <summary>Builds the CalDAV href for an individual event.</summary>
    public static string BuildEventHref(Guid spaceId, Guid eventId)
        => $"/dav/caldav/{spaceId}/{eventId}.ics";

    /// <summary>Builds the CalDAV href for a calendar collection (trailing slash).</summary>
    public static string BuildCalendarHref(Guid spaceId)
        => $"/dav/caldav/{spaceId}/";

    public static CalendarEvent CalendarEntryToICal(CalendarEntry entry)
    {
        var ev = new CalendarEvent()
        {
            DtStart = new CalDateTime(entry.StartDate, !entry.IsAllDay),
            DtEnd = new CalDateTime(entry.EndDate, !entry.IsAllDay),
            Summary = entry.Summary,
            Description = entry.Description,
            Location = entry.Location,
            Uid = entry.Id.ToString(),
            Sequence = entry.Sequence,
            LastModified = new CalDateTime(entry.LastModified)
        };

        if (entry.RecurrenceFrequency != null)
        {
            var recurrence = new RecurrencePattern()
            {
                Frequency = entry.RecurrenceFrequency.Value,
                Count = entry.RecurrenceCount,
                Until = entry.RecurrenceUntil.HasValue ? new CalDateTime(entry.RecurrenceUntil.Value, !entry.IsAllDay) : null
            };

            if (entry.RecurrenceInterval != null)
            {
                recurrence.Interval = entry.RecurrenceInterval.Value;
            }
            
            ev.RecurrenceRules.Add(recurrence);
        }

        return ev;
    }
}
