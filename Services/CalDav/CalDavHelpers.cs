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

    /// <summary>Builds the CalDAV href for an individual event.</summary>
    public static string BuildEventHref(Guid spaceId, Guid eventId)
        => $"/caldav/{spaceId}/{eventId}.ics";

    /// <summary>Builds the CalDAV href for a calendar collection (trailing slash).</summary>
    public static string BuildCalendarHref(Guid spaceId)
        => $"/caldav/{spaceId}/";

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
