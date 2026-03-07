using System.Net;
using System.Text;
using System.Xml.Linq;
using Memoria.Models.Config;
using Microsoft.Extensions.Options;

namespace Memoria.Services.RadicaleClient;

public class RadicaleClient(IHttpClientFactory httpClientFactory, IOptions<RadicaleConfig> config) : IRadicaleClient
{
    private static readonly XNamespace Dav = "DAV:";

    private HttpClient Http => httpClientFactory.CreateClient("radicale");

    private string CalendarUrl(Guid spaceId) => $"{config.Value.BaseUrl}/{spaceId}/calendar/";
    private string ContactsUrl(Guid spaceId) => $"{config.Value.BaseUrl}/{spaceId}/contacts/";
    private string EventUrl(Guid spaceId, Guid eventId) => $"{config.Value.BaseUrl}/{spaceId}/calendar/{eventId}.ics";
    private string ContactUrl(Guid spaceId, Guid contactId) => $"{config.Value.BaseUrl}/{spaceId}/contacts/{contactId}.vcf";

    // -------------------------------------------------------------------------
    // Collection lifecycle
    // -------------------------------------------------------------------------

    public async Task CreateCalendarCollection(Guid spaceId, CancellationToken ct = default)
    {
        var body = """
            <?xml version="1.0" encoding="utf-8"?>
            <mkcol xmlns="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
              <set><prop>
                <resourcetype><collection/><C:calendar/></resourcetype>
              </prop></set>
            </mkcol>
            """;
        await SendMkcol(CalendarUrl(spaceId), body, spaceId, ct);
    }

    public async Task CreateContactsCollection(Guid spaceId, CancellationToken ct = default)
    {
        var body = """
            <?xml version="1.0" encoding="utf-8"?>
            <mkcol xmlns="DAV:" xmlns:CR="urn:ietf:params:xml:ns:carddav">
              <set><prop>
                <resourcetype><collection/><CR:addressbook/></resourcetype>
              </prop></set>
            </mkcol>
            """;
        await SendMkcol(ContactsUrl(spaceId), body, spaceId, ct);
    }

    private async Task SendMkcol(string url, string xmlBody, Guid spaceId, CancellationToken ct)
    {
        var request = Req(new HttpMethod("MKCOL"), url, spaceId);
        request.Content = new StringContent(xmlBody, Encoding.UTF8, "application/xml");
        var response = await Http.SendAsync(request, ct);
        // 201 = created, 405 = already exists — both acceptable
        if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.MethodNotAllowed)
            response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCalendarCollection(Guid spaceId, CancellationToken ct = default)
    {
        var response = await Http.SendAsync(Req(HttpMethod.Delete, CalendarUrl(spaceId), spaceId), ct);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    public async Task DeleteContactsCollection(Guid spaceId, CancellationToken ct = default)
    {
        var response = await Http.SendAsync(Req(HttpMethod.Delete, ContactsUrl(spaceId), spaceId), ct);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    // -------------------------------------------------------------------------
    // Calendar events
    // -------------------------------------------------------------------------

    public Task<List<(Guid Id, string ETag)>> ListEventHrefs(Guid spaceId, CancellationToken ct = default)
        => ListHrefs(CalendarUrl(spaceId), ".ics", spaceId, ct);

    public async Task<string> GetEventIcs(Guid spaceId, Guid eventId, CancellationToken ct = default)
    {
        var response = await Http.SendAsync(Req(HttpMethod.Get, EventUrl(spaceId, eventId), spaceId), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task PutEventIcs(Guid spaceId, Guid eventId, string ics, CancellationToken ct = default)
    {
        var req = Req(HttpMethod.Put, EventUrl(spaceId, eventId), spaceId);
        req.Content = new StringContent(ics, Encoding.UTF8, "text/calendar");
        var response = await Http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteEvent(Guid spaceId, Guid eventId, CancellationToken ct = default)
    {
        var response = await Http.SendAsync(Req(HttpMethod.Delete, EventUrl(spaceId, eventId), spaceId), ct);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    // -------------------------------------------------------------------------
    // Contacts
    // -------------------------------------------------------------------------

    public Task<List<(Guid Id, string ETag)>> ListContactHrefs(Guid spaceId, CancellationToken ct = default)
        => ListHrefs(ContactsUrl(spaceId), ".vcf", spaceId, ct);

    public async Task<string> GetContactVcf(Guid spaceId, Guid contactId, CancellationToken ct = default)
    {
        var response = await Http.SendAsync(Req(HttpMethod.Get, ContactUrl(spaceId, contactId), spaceId), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task PutContactVcf(Guid spaceId, Guid contactId, string vcf, CancellationToken ct = default)
    {
        var req = Req(HttpMethod.Put, ContactUrl(spaceId, contactId), spaceId);
        req.Content = new StringContent(vcf, Encoding.UTF8, "text/vcard");
        var response = await Http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteContact(Guid spaceId, Guid contactId, CancellationToken ct = default)
    {
        var response = await Http.SendAsync(Req(HttpMethod.Delete, ContactUrl(spaceId, contactId), spaceId), ct);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static HttpRequestMessage Req(HttpMethod method, string url, Guid spaceId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("X-Remote-User", spaceId.ToString());
        return req;
    }

    /// <summary>
    /// PROPFIND Depth:1 on a collection URL and extract (Guid Id, string ETag) pairs
    /// for all child resources whose filenames end with <paramref name="extension"/>.
    /// </summary>
    private async Task<List<(Guid Id, string ETag)>> ListHrefs(string collectionUrl, string extension, Guid spaceId, CancellationToken ct)
    {
        var propfind = """
            <?xml version="1.0" encoding="utf-8"?>
            <propfind xmlns="DAV:"><prop><getetag/></prop></propfind>
            """;

        var request = Req(new HttpMethod("PROPFIND"), collectionUrl, spaceId);
        request.Content = new StringContent(propfind, Encoding.UTF8, "application/xml");
        request.Headers.Add("Depth", "1");

        var response = await Http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return [];
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(ct);
        var doc = XDocument.Parse(xml);

        var result = new List<(Guid, string)>();
        foreach (var responseElem in doc.Descendants(Dav + "response"))
        {
            var href = responseElem.Element(Dav + "href")?.Value ?? "";
            if (!href.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;

            var filename = href.Split('/').LastOrDefault() ?? "";
            var idStr = filename[..^extension.Length];
            if (!Guid.TryParse(idStr, out var id)) continue;

            var etag = responseElem.Descendants(Dav + "getetag").FirstOrDefault()?.Value ?? "";
            result.Add((id, etag.Trim('"')));
        }

        return result;
    }
}
