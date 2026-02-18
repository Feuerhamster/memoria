using System.Xml.Linq;
using Memoria.Models.Database;
using Memoria.Models.WebDav;

namespace Memoria.Services.CalDav;

/// <summary>
/// Builds CalDAV-specific XML elements for multistatus responses.
/// Follows the same structural pattern as <c>WebDavXmlBuilder</c>.
/// </summary>
public static class CalDavXmlBuilder
{
    private static readonly XNamespace Dav = "DAV:";
    private static readonly XNamespace Cal = "urn:ietf:params:xml:ns:caldav";
    private static readonly XNamespace Apple = "http://apple.com/ns/ical/";

    public static XElement BuildRootResponse()
    {
        return new XElement(Dav + "response",
            new XElement(Dav + "href", "/dav/caldav/"),
            new XElement(Dav + "propstat",
                new XElement(Dav + "prop",
                    new XElement(Dav + "displayname", "Calendars"),
                    new XElement(Dav + "resourcetype",
                        new XElement(Dav + "collection"),
                        new XElement(Dav + "principal")
                    ),
                    new XElement(Dav + "current-user-principal",
                        new XElement(Dav + "href", "/dav/caldav/principals/me/")
                    ),
                    new XElement(Dav + "supported-report-set",
                        new XElement(Dav + "supported-report",
                            new XElement(Dav + "report", new XElement(Cal + "calendar-query"))
                        )
                    )
                ),
                new XElement(Dav + "status", "HTTP/1.1 200 OK")
            )
        );
    }
    
    // -------------------------------------------------------------------------
    // Calendar collection
    // -------------------------------------------------------------------------

    /// <summary>Creates a DAV:response element describing a calendar collection (i.e. a Space).</summary>
    public static XElement CreateCalendarCollection(string href, Space space)
    {
        var props = new List<XElement?>
        {
            new XElement(Dav + "displayname", space.Name),
            new XElement(Dav + "resourcetype",
                new XElement(Dav + "collection"),
                new XElement(Cal + "calendar")
            ),
            new XElement(Cal + "calendar-description", space.Description ?? string.Empty),
            new XElement(Dav + "creationdate", space.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")),
            new XElement(Dav + "getlastmodified", space.CreatedAt.ToString("R")),
            new XElement(Cal + "supported-calendar-component-set",
                new XElement(Cal + "comp", new XAttribute("name", "VEVENT"))
            ),
            new XElement(Dav + "current-user-privilege-set",
                new XElement(Dav + "privilege", new XElement(Dav + "read")),
                new XElement(Dav + "privilege", new XElement(Dav + "write")),
                new XElement(Dav + "privilege", new XElement(Dav + "write-content")),
                new XElement(Dav + "privilege", new XElement(Dav + "write-properties")),
                new XElement(Dav + "privilege", new XElement(Dav + "bind")),
                new XElement(Dav + "privilege", new XElement(Dav + "unbind"))
            ),
        };

        if (!string.IsNullOrEmpty(space.Color))
            props.Add(new XElement(Apple + "calendar-color", space.Color));

        return new XElement(Dav + "response",
            new XElement(Dav + "href", href),
            new XElement(Dav + "propstat",
                new XElement(Dav + "prop", props.Where(p => p != null)),
                new XElement(Dav + "status", "HTTP/1.1 200 OK")
            )
        );
    }

    // -------------------------------------------------------------------------
    // Calendar event
    // -------------------------------------------------------------------------

    /// <summary>Creates a DAV:response element for a single calendar event resource.</summary>
    public static XElement CreateEventResponse(
        Guid spaceId,
        CaldavEventMetadata meta,
        string? icalData,
        bool includeCalendarData = true)
    {
        var etag = CalDavHelpers.GenerateETag(meta);
        var href = CalDavHelpers.BuildEventHref(spaceId, meta.Id);
        
        var props = new List<XElement>
        {
            new XElement(Dav + "getetag", $"\"{etag}\""),
            new XElement(Dav + "getcontenttype", "text/calendar; charset=utf-8"),
            new XElement(Dav + "resourcetype"),  // empty = not a collection
            new XElement(Dav + "getlastmodified", meta.LastModified.ToString("R")),
        };

        if (includeCalendarData)
            props.Add(new XElement(Cal + "calendar-data", icalData));

        return new XElement(Dav + "response",
            new XElement(Dav + "href", href),
            new XElement(Dav + "propstat",
                new XElement(Dav + "prop", props),
                new XElement(Dav + "status", "HTTP/1.1 200 OK")
            )
        );
    }

    // -------------------------------------------------------------------------
    // REPORT parsing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a calendar-query REPORT body and returns the optional time-range filter.
    /// Returns null when the XML cannot be parsed as a known REPORT type.
    /// </summary>
    public static CalendarQueryFilter? ParseCalendarQuery(XDocument doc)
    {
        var root = doc.Root;
        if (root == null) return null;

        DateTime? start = null;
        DateTime? end = null;

        // Look for time-range anywhere in the filter tree
        var timeRangeEl = root.Descendants(Cal + "time-range").FirstOrDefault();
        if (timeRangeEl == null) return new CalendarQueryFilter(start, end);
        var startAttr = timeRangeEl.Attribute("start")?.Value;
        var endAttr = timeRangeEl.Attribute("end")?.Value;

        if (startAttr != null && TryParseIcalDate(startAttr, out var s)) start = s;
        if (endAttr != null && TryParseIcalDate(endAttr, out var e)) end = e;

        return new CalendarQueryFilter(start, end);
    }

    // -------------------------------------------------------------------------
    // MultiStatus document
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a 207 Multi-Status response document from a list of DAV:response elements.
    /// </summary>
    public static string BuildMultiStatus(List<XElement> responses)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Dav + "multistatus",
                new XAttribute(XNamespace.Xmlns + "D", "DAV:"),
                new XAttribute(XNamespace.Xmlns + "C", Cal.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "A", Apple.NamespaceName),
                responses
            )
        );

        return doc.Declaration + Environment.NewLine + doc.Root!.ToString(SaveOptions.DisableFormatting);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool TryParseIcalDate(string value, out DateTime result)
    {
        // CalDAV uses yyyyMMddTHHmmssZ or yyyy-MM-ddTHH:mm:ssZ
        return DateTime.TryParseExact(value,
            ["yyyyMMddTHHmmssZ", "yyyy-MM-ddTHH:mm:ssZ", "yyyyMMdd"],
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out result);
    }
}

/// <summary>Time-range filter parsed from a calendar-query REPORT.</summary>
public record CalendarQueryFilter(DateTime? Start, DateTime? End);
