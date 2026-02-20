using Ical.Net;
using Ical.Net.Serialization;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Services.CalDav;
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
public class CalendarFeedController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns all public calendar entries of a space as a subscribable iCal feed.
    /// Only events with <see cref="RessourceAccessPolicy.Public"/> are included.
    /// </summary>
    [HttpGet("{spaceId:guid}/public.ics")]
    public async Task<IActionResult> GetPublicFeed(Guid spaceId, CancellationToken ct)
    {
        var space = await db.Spaces
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spaceId, ct);

        if (space == null) return NotFound();

        var events = await db.CalendarEvents
            .AsNoTracking()
            .Where(e => e.SpaceId == spaceId && e.AccessPolicy == RessourceAccessPolicy.Public)
            .ToListAsync(ct);

        var calendar = new Ical.Net.Calendar();
        calendar.AddProperty("X-WR-CALNAME", space.Name);
        if (!string.IsNullOrEmpty(space.Description))
            calendar.AddProperty("X-WR-CALDESC", space.Description);
        calendar.AddProperty("X-WR-TIMEZONE", "UTC");

        foreach (var entry in events)
            calendar.Events.Add(CalDavHelpers.CalendarEntryToICal(entry));

        var ics = new CalendarSerializer().SerializeToString(calendar);
        if (ics == null) return StatusCode(StatusCodes.Status500InternalServerError);

        return Content(ics, "text/calendar; charset=utf-8");
    }
}
