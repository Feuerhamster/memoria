using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Memoria.Models;
using Memoria.Services;
using Memoria.Services.RadicaleClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers.CalendarFeed;

/// <summary>
/// Provides a public, unauthenticated iCal feed for a space's public calendar entries.
/// Suitable for subscription URLs in external calendar clients (e.g. Google Calendar, Apple Calendar).
/// </summary>
[Route("calendar")]
[AllowAnonymous]
public class CalendarFeedController(AppDbContext db, IRadicaleClient radicale) : ControllerBase
{
    /// <summary>
    /// Returns all public calendar entries of a space as a subscribable iCal feed.
    /// Only events with <see cref="RessourceAccessPolicy.Public"/> access policy are included.
    /// </summary>
    [HttpGet("{spaceId:guid}/public.ics")]
    public async Task<IActionResult> GetPublicFeed(Guid spaceId, CancellationToken ct)
    {
        var space = await db.Spaces
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spaceId, ct);

        if (space == null) return NotFound();

        var publicEntries = await db.CalendarEventCache
            .AsNoTracking()
            .Where(e => e.SpaceId == spaceId && e.AccessPolicy == RessourceAccessPolicy.Public)
            .ToListAsync(ct);

        var calendar = new Ical.Net.Calendar();
        calendar.AddProperty("X-WR-CALNAME", space.Name);
        if (!string.IsNullOrEmpty(space.Description))
            calendar.AddProperty("X-WR-CALDESC", space.Description);
        calendar.AddProperty("X-WR-TIMEZONE", "UTC");

        foreach (var entry in publicEntries)
        {
            try
            {
                var ics = await radicale.GetEventIcs(spaceId, entry.Id, ct);
                var parsed = Ical.Net.Calendar.Load(ics);
                var ev = parsed?.Events.FirstOrDefault();
                if (ev != null) calendar.Events.Add(ev);
            }
            catch
            {
                // Skip events that can't be retrieved from Radicale
            }
        }

        var result = new CalendarSerializer().SerializeToString(calendar);
        if (result == null) return StatusCode(StatusCodes.Status500InternalServerError);

        return Content(result, "text/calendar; charset=utf-8");
    }
}
