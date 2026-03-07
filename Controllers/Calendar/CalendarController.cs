using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Models.Response;
using Memoria.Services;
using Memoria.Services.RadicaleClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers.Calendar;

[ApiController]
[Route("/api/spaces/{spaceId:guid}/calendar")]
[Authorize]
public class CalendarController(
    AppDbContext db,
    IAccessPolicyHelperService accessControl,
    IRadicaleClient radicale) : ControllerBase
{
    // -------------------------------------------------------------------------
    // GET /api/spaces/{spaceId}/calendar — list events
    // -------------------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<List<CalendarEventDto>>> ListEvents(Guid spaceId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var isMember = await accessControl.CheckSpaceMembership(spaceId, userId, ct);
        var maxPolicy = isMember ? RessourceAccessPolicy.Members : RessourceAccessPolicy.Shared;

        var cacheEntries = await db.CalendarEventCache
            .Where(e => e.SpaceId == spaceId
                        && (e.AccessPolicy <= maxPolicy || e.OwnerUserId == userId))
            .ToListAsync(ct);

        var result = new List<CalendarEventDto>();
        foreach (var entry in cacheEntries)
        {
            try
            {
                var ics = await radicale.GetEventIcs(spaceId, entry.Id, ct);
                var dto = ParseIcsToDto(entry, ics);
                if (dto != null) result.Add(dto);
            }
            catch
            {
                // Skip events that can't be retrieved from Radicale
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // GET /api/spaces/{spaceId}/calendar/{id} — get single event
    // -------------------------------------------------------------------------

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> GetEvent(Guid spaceId, Guid id, CancellationToken ct)
    {
        var cache = await db.CalendarEventCache
            .FirstOrDefaultAsync(e => e.SpaceId == spaceId && e.Id == id, ct);
        if (cache == null) return new NotFoundApiException();

        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Read, User))
            return new ActionNotAllowedApiException();

        var ics = await radicale.GetEventIcs(spaceId, id, ct);
        var dto = ParseIcsToDto(cache, ics);
        return dto != null ? Ok(dto) : new OperationFailedApiException();
    }

    // -------------------------------------------------------------------------
    // POST /api/spaces/{spaceId}/calendar — create event
    // -------------------------------------------------------------------------

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> CreateEvent(
        Guid spaceId, CreateEventRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await accessControl.CheckSpaceMembership(spaceId, userId, ct))
            return new ActionNotAllowedApiException();

        var eventId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var ics = BuildIcs(eventId, request.Summary, request.Description, request.Location,
            request.StartDate, request.EndDate, request.IsAllDay, sequence: 0, created: now);

        await radicale.PutEventIcs(spaceId, eventId, ics, ct);

        var cache = new CalendarEventCache
        {
            Id = eventId,
            SpaceId = spaceId,
            OwnerUserId = userId,
            LastModified = now
        };
        db.CalendarEventCache.Add(cache);
        await db.SaveChangesAsync(ct);

        return Created($"/api/spaces/{spaceId}/calendar/{eventId}",
            new CalendarEventDto
            {
                Id = eventId,
                SpaceId = spaceId,
                OwnerUserId = userId,
                Summary = request.Summary,
                Description = request.Description,
                Location = request.Location,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsAllDay = request.IsAllDay,
                LastModified = now
            });
    }

    // -------------------------------------------------------------------------
    // PATCH /api/spaces/{spaceId}/calendar/{id} — update event
    // -------------------------------------------------------------------------

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> UpdateEvent(
        Guid spaceId, Guid id, UpdateEventRequest request, CancellationToken ct)
    {
        var cache = await db.CalendarEventCache
            .FirstOrDefaultAsync(e => e.SpaceId == spaceId && e.Id == id, ct);
        if (cache == null) return new NotFoundApiException();

        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Write, User))
            return new ActionNotAllowedApiException();

        // Load current ICS from Radicale, patch fields, put back
        var existingIcs = await radicale.GetEventIcs(spaceId, id, ct);
        var calendar = Ical.Net.Calendar.Load(existingIcs);
        var ev = calendar?.Events.FirstOrDefault();
        if (ev == null) return new OperationFailedApiException();

        if (request.Summary != null) ev.Summary = request.Summary;
        if (request.Description != null) ev.Description = request.Description;
        if (request.Location != null) ev.Location = request.Location;
        if (request.StartDate.HasValue)
        {
            var allDay = request.IsAllDay ?? (ev.DtStart is { HasTime: false });
            ev.DtStart = new CalDateTime(request.StartDate.Value, !allDay);
        }
        if (request.EndDate.HasValue)
        {
            var allDay = request.IsAllDay ?? (ev.DtEnd is { HasTime: false });
            ev.DtEnd = new CalDateTime(request.EndDate.Value, !allDay);
        }
        ev.Sequence++;
        ev.LastModified = new CalDateTime(DateTime.UtcNow);

        var updatedIcs = new CalendarSerializer().SerializeToString(calendar!);
        await radicale.PutEventIcs(spaceId, id, updatedIcs, ct);

        cache.LastModified = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var dto = ParseIcsToDto(cache, updatedIcs);
        return dto != null ? Ok(dto) : new OperationFailedApiException();
    }

    // -------------------------------------------------------------------------
    // DELETE /api/spaces/{spaceId}/calendar/{id} — delete event
    // -------------------------------------------------------------------------

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid spaceId, Guid id, CancellationToken ct)
    {
        var cache = await db.CalendarEventCache
            .FirstOrDefaultAsync(e => e.SpaceId == spaceId && e.Id == id, ct);
        if (cache == null) return new NotFoundApiException();

        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Write, User))
            return new ActionNotAllowedApiException();

        await radicale.DeleteEvent(spaceId, id, ct);

        db.CalendarEventCache.Remove(cache);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildIcs(Guid eventId, string? summary, string? description,
        string? location, DateTime start, DateTime end, bool isAllDay, int sequence, DateTime created)
    {
        var ev = new CalendarEvent
        {
            Uid = eventId.ToString(),
            Summary = summary,
            Description = description,
            Location = location,
            DtStart = new CalDateTime(start, !isAllDay),
            DtEnd = new CalDateTime(end, !isAllDay),
            Sequence = sequence,
            Created = new CalDateTime(created),
            LastModified = new CalDateTime(created)
        };

        var cal = new global::Ical.Net.Calendar { Events = { ev } };
        return new CalendarSerializer().SerializeToString(cal);
    }

    private static CalendarEventDto? ParseIcsToDto(CalendarEventCache cache, string ics)
    {
        try
        {
            var calendar = Ical.Net.Calendar.Load(ics);
            var ev = calendar?.Events.FirstOrDefault();
            if (ev == null) return null;

            return new CalendarEventDto
            {
                Id = cache.Id,
                SpaceId = cache.SpaceId,
                OwnerUserId = cache.OwnerUserId,
                Summary = ev.Summary,
                Description = ev.Description,
                Location = ev.Location,
                StartDate = ev.DtStart.AsUtc,
                EndDate = (ev.DtEnd ?? ev.DtStart).AsUtc,
                IsAllDay = ev.DtStart is { HasTime: false },
                ETag = cache.ETag,
                LastModified = cache.LastModified
            };
        }
        catch
        {
            return null;
        }
    }
}
