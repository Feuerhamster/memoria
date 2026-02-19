using EFCoreSecondLevelCacheInterceptor;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.WebDav;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Services;

public interface ICalendarService
{
    public Task<CalendarEntry?> GetCalendarEntry(Guid id, Guid spaceId, CancellationToken ct);
    public Task<List<CalendarEntry>> FindPotentialCalendarEntriesInRange(Guid spaceId, Guid userId, RessourceAccessPolicy maxPolicy, DateTime? start, DateTime? end, CancellationToken ct);
    public Task<List<CaldavEventMetadata>> GetAllCalendarEventsMetadata(Guid spaceId, Guid userId, RessourceAccessPolicy maxPolicy, CancellationToken ct);
    /// <summary>Returns a CTag string for a single calendar. Changes whenever any event in the space is modified.</summary>
    public Task<string> GetCalendarCtag(Guid spaceId, CancellationToken ct);
    /// <summary>Returns CTag strings for multiple calendars in one query.</summary>
    public Task<Dictionary<Guid, string>> GetCalendarCtags(IEnumerable<Guid> spaceIds, CancellationToken ct);
}

public class CalendarService(AppDbContext db, ISpaceService spaceService) : ICalendarService
{
    public Task<CalendarEntry?> GetCalendarEntry(Guid id, Guid spaceId, CancellationToken ct)
    {
        return db.CalendarEvents.FirstOrDefaultAsync(e => e.SpaceId == spaceId && e.Id == id, ct);
    }

    public Task<List<CalendarEntry>> FindPotentialCalendarEntriesInRange(Guid spaceId, Guid userId, RessourceAccessPolicy maxPolicy, DateTime? start, DateTime? end, CancellationToken ct)
    {
        return db.CalendarEvents
            .Where(e =>
                e.SpaceId == spaceId
                && (e.AccessPolicy <= maxPolicy || e.OwnerUserId == userId)
                && (
                    // No time filter at all — return everything (calendar-query without time-range)
                    (start == null && end == null)
                    ||
                    // Recurring: include if the rule could produce occurrences inside the window
                    (e.RecurrenceFrequency != null
                        && (end == null || e.StartDate <= end)
                        && (start == null || e.RecurrenceUntil == null || e.RecurrenceUntil >= start))
                    ||
                    // Single events: standard overlap check (event starts before range ends, ends after range starts)
                    (e.RecurrenceFrequency == null
                        && (end == null || e.StartDate < end)
                        && (start == null || e.EndDate > start))
                )
            )
            .ToListAsync(ct);
    }

    public async Task<List<CaldavEventMetadata>> GetAllCalendarEventsMetadata(Guid spaceId, Guid userId, RessourceAccessPolicy maxPolicy, CancellationToken ct)
    {
        var events = await db.CalendarEvents.Cacheable().Select(e => new { e.Id, e.Sequence, e.LastModified, e.SpaceId, e.OwnerUserId, e.AccessPolicy })
            .Where(e => e.SpaceId == spaceId && (e.AccessPolicy <= maxPolicy || e.OwnerUserId == userId)).ToListAsync(ct);

        return events.Select(e => new CaldavEventMetadata(e.Id, e.Sequence, e.LastModified)).ToList();
    }

    public async Task<string> GetCalendarCtag(Guid spaceId, CancellationToken ct)
    {
        var latest = await db.CalendarEvents
            .Cacheable()
            .Where(e => e.SpaceId == spaceId)
            .MaxAsync(e => (DateTime?)e.LastModified, ct);
        return (latest ?? DateTime.MinValue).Ticks.ToString();
    }

    public async Task<Dictionary<Guid, string>> GetCalendarCtags(IEnumerable<Guid> spaceIds, CancellationToken ct)
    {
        var ids = spaceIds.ToList();
        var rows = await db.CalendarEvents
            .Cacheable()
            .Where(e => ids.Contains(e.SpaceId))
            .GroupBy(e => e.SpaceId)
            .Select(g => new { SpaceId = g.Key, Latest = g.Max(e => e.LastModified) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.SpaceId, r => r.Latest.Ticks.ToString());
    }
}