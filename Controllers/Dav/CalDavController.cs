using System.Xml.Linq;
using EFCoreSecondLevelCacheInterceptor;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Memoria.Attributes;
using Memoria.Authentication;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.WebDav;
using Memoria.Services;
using Memoria.Services.CalDav;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers.Dav;

/// <summary>
/// CalDAV server implementation (RFC 4791).
/// Each Space is exposed as a calendar collection.
///
/// URL structure:
///   /caldav/                        OPTIONS, PROPFIND (list calendars)
///   /caldav/{spaceId}/              PROPFIND, REPORT
///   /caldav/{spaceId}/{eventId}.ics GET, PUT, DELETE
/// </summary>
[Route("dav/caldav")]
[Authorize(AuthenticationSchemes = BasicAuthHandler.SchemeName, Policy = "CalDav")]
[EnsureWwwAuthenticate]
public class CalDavController(
    AppDbContext db,
    ICalendarService calendarService,
    IAccessPolicyHelperService accessControl,
    ISpaceService spaceService) : ControllerBase
{
    // -------------------------------------------------------------------------
    // OPTIONS
    // -------------------------------------------------------------------------

    [AcceptVerbs("OPTIONS")]
    [AllowAnonymous]
    public IActionResult Options()
    {
        Response.Headers["DAV"] = "1, 2, calendar-access";
        Response.Headers.Allow = "OPTIONS, PROPFIND, REPORT, GET, PUT, DELETE";
        return Ok();
    }

    // -------------------------------------------------------------------------
    // PROPFIND /caldav/ — list accessible spaces as calendar collections
    // -------------------------------------------------------------------------

    [AcceptVerbs("PROPFIND")]
    [ValidateWebDavDepth]
    [Route("")]
    public async Task<IActionResult> ListCalendars(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var depth = GetDepth();

        var responses = new List<XElement>
        {
            CalDavXmlBuilder.BuildRootResponse()
        };

        if (depth < 1) return MultiStatus(responses);
        
        var spaces = await spaceService.GetMemberSpaces(userId);
        
        foreach (var space in spaces)
        {
            var href = CalDavHelpers.BuildCalendarHref(space.Id);
            responses.Add(CalDavXmlBuilder.CreateCalendarCollection(href, space));
        }

        return MultiStatus(responses);
    }

    // -------------------------------------------------------------------------
    // OPTIONS /caldav/{spaceId}/ and /caldav/{spaceId}/{eventId}.ics
    // GNOME Calendar checks these to determine if the calendar is writable.
    // -------------------------------------------------------------------------

    [AcceptVerbs("OPTIONS")]
    [Route("{spaceId:guid}")]
    public IActionResult SpaceOptions()
    {
        Response.Headers["DAV"] = "1, 2, calendar-access";
        Response.Headers.Allow = "OPTIONS, PROPFIND, REPORT, GET, PUT, DELETE";
        return Ok();
    }

    [AcceptVerbs("OPTIONS")]
    [Route("{spaceId:guid}/{eventId}.ics")]
    public IActionResult EventOptions()
    {
        Response.Headers["DAV"] = "1, 2, calendar-access";
        Response.Headers.Allow = "OPTIONS, GET, PUT, DELETE";
        return Ok();
    }

    // -------------------------------------------------------------------------
    // PROPFIND /caldav/principals/me/ — principal resource (calendar-home-set)
    // -------------------------------------------------------------------------

    [AcceptVerbs("PROPFIND")]
    [Route("principals/me")]
    public IActionResult GetPrincipal()
    {
        var dav = XNamespace.Get("DAV:");
        var cal = XNamespace.Get("urn:ietf:params:xml:ns:caldav");

        var response = new XElement(dav + "response",
            new XElement(dav + "href", "/dav/caldav/principals/me/"),
            new XElement(dav + "propstat",
                new XElement(dav + "prop",
                    new XElement(dav + "resourcetype", new XElement(dav + "principal")),
                    new XElement(dav + "principal-URL",
                        new XElement(dav + "href", "/dav/caldav/principals/me/")
                    ),
                    new XElement(cal + "calendar-home-set",
                        new XElement(dav + "href", "/dav/caldav/")
                    )
                ),
                new XElement(dav + "status", "HTTP/1.1 200 OK")
            )
        );

        return MultiStatus([response]);
    }

    // -------------------------------------------------------------------------
    // PROPFIND /caldav/{spaceId}/ — list events in calendar
    // -------------------------------------------------------------------------

    [AcceptVerbs("PROPFIND")]
    [ValidateWebDavDepth]
    [Route("{spaceId:guid}")]
    public async Task<IActionResult> ListEvents(Guid spaceId, CancellationToken ct)
    {
        var space = await GetSpace(spaceId, ct);
        if (space == null) return NotFound();

        var userId = User.GetUserId();
        if (!await accessControl.CheckSpaceMembership(space.Id, userId, ct))
            return Forbid();

        var depth = GetDepth();
        var calendarHref = CalDavHelpers.BuildCalendarHref(space.Id);
        var responses = new List<XElement>
        {
            CalDavXmlBuilder.CreateCalendarCollection(calendarHref, space)
        };

        if (depth < 1) return MultiStatus(responses);
        
        var eventsMeta = await calendarService.GetAllCalendarEventsMetadata(space.Id, userId, ct);

        responses.AddRange(eventsMeta.Select(e => CalDavXmlBuilder.CreateEventResponse(space.Id, e, null, false)).ToList());

        return MultiStatus(responses);
    }

    // -------------------------------------------------------------------------
    // REPORT /caldav/{spaceName}/ — calendar-query
    // -------------------------------------------------------------------------

    [AcceptVerbs("REPORT")]
    [Route("{spaceId:guid}")]
    public async Task<IActionResult> CalendarQuery(Guid spaceId, CancellationToken ct)
    {
        var space = await GetSpace(spaceId, ct);
        if (space == null) return NotFound();

        var userId = User.GetUserId();
        if (!await accessControl.CheckSpaceMembership(space.Id, userId, ct))
            return Forbid();

        // Parse REPORT body
        CalendarQueryFilter? filter = null;
        try
        {
            Request.EnableBuffering();
            var doc = await XDocument.LoadAsync(Request.Body, LoadOptions.None, ct);
            filter = CalDavXmlBuilder.ParseCalendarQuery(doc);
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        var events = await calendarService.FindPotentialCalendarEntriesInRange(space.Id, userId, filter.Start, filter.End, ct);
        
        var responses = events.Select(e =>
        {
            var calendar = new Calendar()
            {
                Events = { CalDavHelpers.CalendarEntryToICal(e) }
            };

            var ics =  new CalendarSerializer().SerializeToString(calendar);
            return CalDavXmlBuilder.CreateEventResponse(space.Id, new CaldavEventMetadata(e),  ics);
        }).ToList();

        return MultiStatus(responses);
    }

    // -------------------------------------------------------------------------
    // GET /caldav/{spaceName}/{eventId}.ics
    // -------------------------------------------------------------------------

    [HttpGet("{spaceId:guid}/{eventId}.ics")]
    public async Task<IActionResult> GetEvent(Guid spaceId, Guid eventId, CancellationToken ct)
    {
        var space = await GetSpace(spaceId, ct);
        if (space == null) return NotFound();

        var entry = await calendarService.GetCalendarEntry(eventId, spaceId, ct);
        if (entry == null) return NotFound();

        if (!await accessControl.CheckAccessPolicy(entry, AccessIntent.Read, User))
            return Forbid();

        var etag = CalDavHelpers.GenerateETag(new CaldavEventMetadata(entry));
        Response.Headers.ETag = $"\"{etag}\"";
        Response.Headers.LastModified = entry.LastModified.ToString("R");

        var icalData = CalDavHelpers.CalendarEntryToICal(entry);

        var calendar = new Calendar()
        {
            Events = { icalData }
        };
        
        var calendarSerializer = new CalendarSerializer();
        var generatedIcs = calendarSerializer.SerializeToString(calendar);
        
        if (generatedIcs == null) return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        
        return Content(generatedIcs, "text/calendar; charset=utf-8");
    }

    // -------------------------------------------------------------------------
    // PUT /caldav/{spaceName}/{eventId}.ics — create or update
    // -------------------------------------------------------------------------

    [HttpPut("{spaceId:guid}/{eventId}.ics")]
    public async Task<IActionResult> PutEvent(Guid spaceId, Guid eventId, CancellationToken ct)
    {
        var space = await GetSpace(spaceId, ct);
        if (space == null) return NotFound();

        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync(ct);

        Calendar calendar;
        try
        {
            calendar = Calendar.Load(body);
        }
        catch
        {
            return BadRequest();
        }

        var icalEvent = calendar.Events.FirstOrDefault();
        if (icalEvent == null) return BadRequest();

        var userId = User.GetUserId();
        var existing = await calendarService.GetCalendarEntry(eventId, spaceId, ct);

        if (existing != null)
        {
            if (!await accessControl.CheckAccessPolicy(existing, AccessIntent.Write, User))
                return Forbid();

            ApplyIcalUpdate(existing, ParseIcalEvent(icalEvent));
            await db.SaveChangesAsync(ct);

            var etag = CalDavHelpers.GenerateETag(new CaldavEventMetadata(existing));
            Response.Headers.ETag = $"\"{etag}\"";
            return NoContent();
        }
        else
        {
            if (!await accessControl.CheckSpaceMembership(spaceId, userId, ct))
                return Forbid();

            var now = DateTime.UtcNow;
            var entry = new CalendarEntry
            {
                Id = eventId,
                SpaceId = spaceId,
                OwnerUserId = userId,
                CreatedAt = now,
                LastModified = now,
                Sequence = 0
            };

            var parsed = ParseIcalEvent(icalEvent);
            entry.Summary = parsed.Summary;
            entry.Description = parsed.Description;
            entry.Location = parsed.Location;
            entry.StartDate = parsed.StartDate;
            entry.EndDate = parsed.EndDate;
            entry.IsAllDay = parsed.IsAllDay;
            entry.RecurrenceFrequency = parsed.RecurrenceFrequency;
            entry.RecurrenceInterval = parsed.RecurrenceInterval;
            entry.RecurrenceCount = parsed.RecurrenceCount;
            entry.RecurrenceUntil = parsed.RecurrenceUntil;

            db.CalendarEvents.Add(entry);
            await db.SaveChangesAsync(ct);

            var etag = CalDavHelpers.GenerateETag(new CaldavEventMetadata(entry));
            Response.Headers.ETag = $"\"{etag}\"";
            return Created(CalDavHelpers.BuildEventHref(spaceId, eventId), null);
        }
    }

    // -------------------------------------------------------------------------
    // DELETE /caldav/{spaceName}/{eventId}.ics
    // -------------------------------------------------------------------------

    [HttpDelete("{spaceId:guid}/{eventId}.ics")]
    public async Task<IActionResult> DeleteEvent(Guid spaceId, Guid eventId, CancellationToken ct)
    {
        var space = await GetSpace(spaceId, ct);
        if (space == null) return NotFound();

        var evt = await calendarService.GetCalendarEntry(eventId, spaceId, ct);
        if (evt == null) return NotFound();

        if (!await accessControl.CheckAccessPolicy(evt, AccessIntent.Write, User))
            return Forbid();

        db.CalendarEvents.Remove(evt);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<Space?> GetSpace(Guid spaceId, CancellationToken ct)
        => await db.Spaces
            .Cacheable()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spaceId, ct);

    private int GetDepth() => Request.Headers["Depth"].FirstOrDefault() switch
    {
        "0" => 0,
        "1" => 1,
        _ => 1
    };

    private IActionResult MultiStatus(List<XElement> responses)
    {
        var body = CalDavXmlBuilder.BuildMultiStatus(responses);
        Response.StatusCode = 207;
        Response.ContentType = "application/xml; charset=utf-8";
        return Content(body, "application/xml; charset=utf-8");
    }

    /// <summary>Parses an iCal CalendarEvent into a transient CalendarEntry (no Id/SpaceId/Owner set).</summary>
    private static CalendarEntry ParseIcalEvent(CalendarEvent ev)
    {
        var isAllDay = ev.DtStart is { HasTime: false };
        var entry = new CalendarEntry
        {
            Summary = ev.Summary,
            Description = ev.Description,
            Location = ev.Location,
            StartDate = DateTime.SpecifyKind(ev.DtStart.Value, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind((ev.DtEnd ?? ev.DtStart).Value, DateTimeKind.Utc),
            IsAllDay = isAllDay,
        };

        var rrule = ev.RecurrenceRules.FirstOrDefault();
        if (rrule != null)
        {
            entry.RecurrenceFrequency = rrule.Frequency;
            entry.RecurrenceInterval = rrule.Interval != 1 ? rrule.Interval : null;
            entry.RecurrenceCount = rrule.Count > 0 ? rrule.Count : null;
            entry.RecurrenceUntil = rrule.Until != null
                ? DateTime.SpecifyKind(rrule.Until.Value, DateTimeKind.Utc)
                : null;
        }

        return entry;
    }

    /// <summary>Applies fields from a parsed iCal event onto an existing tracked entity.</summary>
    private static void ApplyIcalUpdate(CalendarEntry target, CalendarEntry source)
    {
        target.Summary = source.Summary;
        target.Description = source.Description;
        target.Location = source.Location;
        target.StartDate = source.StartDate;
        target.EndDate = source.EndDate;
        target.IsAllDay = source.IsAllDay;
        target.RecurrenceFrequency = source.RecurrenceFrequency;
        target.RecurrenceUntil = source.RecurrenceUntil;
        target.RecurrenceCount = source.RecurrenceCount;
        target.RecurrenceInterval = source.RecurrenceInterval;
        target.LastModified = DateTime.UtcNow;
        target.Sequence++;
    }
}
