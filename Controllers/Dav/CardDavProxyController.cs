using System.Net;
using Memoria.Attributes;
using Memoria.Authentication;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoria.Controllers.Dav;

/// <summary>
/// Proxy for native CardDAV clients. Root PROPFIND and principal discovery are answered
/// locally; everything else is forwarded to Radicale with URL rewriting.
/// </summary>
[Route("dav/carddav")]
[Authorize(AuthenticationSchemes = BasicAuthHandler.SchemeName, Policy = "CardDav")]
[EnsureWwwAuthenticate]
public class CardDavProxyController(
    AppDbContext db,
    IAccessPolicyHelperService accessControl,
    ISpaceService spaceService,
    IOptions<RadicaleConfig> radicaleConfig,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private HttpClient RadicaleHttp => httpClientFactory.CreateClient("radicale");
    private string RadicaleBase => radicaleConfig.Value.BaseUrl;

    // -------------------------------------------------------------------------
    // OPTIONS — answer locally for all paths
    // -------------------------------------------------------------------------

    [AcceptVerbs("OPTIONS")]
    [AllowAnonymous]
    [Route("{**path}")]
    public IActionResult Options()
    {
        Response.Headers["DAV"] = "1, 2, 3, addressbook";
        Response.Headers.Allow = "OPTIONS, PROPFIND, REPORT, GET, PUT, DELETE";
        return Ok();
    }

    // -------------------------------------------------------------------------
    // PROPFIND / — list member spaces as address book collections (local)
    // -------------------------------------------------------------------------

    [AcceptVerbs("PROPFIND")]
    [Route("")]
    public async Task<IActionResult> Root(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var depth = Request.Headers["Depth"].FirstOrDefault();
        var spaces = depth == "0" ? [] : await spaceService.GetMemberSpaces(userId);

        var collections = string.Concat(spaces.Select(s => $"""
            <response>
              <href>/dav/carddav/{s.Id}/</href>
              <propstat><prop>
                <displayname>{X(s.Name)}</displayname>
                <resourcetype><collection/><CR:addressbook/></resourcetype>
              </prop><status>HTTP/1.1 200 OK</status></propstat>
            </response>
            """));

        return MultiStatus($"""
            <response>
              <href>/dav/carddav/</href>
              <propstat><prop>
                <displayname>Address Books</displayname>
                <resourcetype><collection/></resourcetype>
                <current-user-principal><href>/dav/carddav/principals/me/</href></current-user-principal>
              </prop><status>HTTP/1.1 200 OK</status></propstat>
            </response>
            {collections}
            """);
    }

    // -------------------------------------------------------------------------
    // PROPFIND /principals/me — principal resource (local)
    // -------------------------------------------------------------------------

    [AcceptVerbs("PROPFIND")]
    [Route("principals/me")]
    public IActionResult Principal() => MultiStatus("""
        <response>
          <href>/dav/carddav/principals/me/</href>
          <propstat><prop>
            <resourcetype><principal/></resourcetype>
            <CR:addressbook-home-set><href>/dav/carddav/</href></CR:addressbook-home-set>
          </prop><status>HTTP/1.1 200 OK</status></propstat>
        </response>
        """);

    // -------------------------------------------------------------------------
    // PROPFIND /{spaceId}/ — access check → forward to Radicale
    // -------------------------------------------------------------------------

    [AcceptVerbs("PROPFIND")]
    [Route("{spaceId:guid}")]
    public async Task<IActionResult> PropFind(Guid spaceId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var space = await db.Spaces.AsNoTracking().FirstOrDefaultAsync(s => s.Id == spaceId, ct);
        if (space == null) return NotFound();

        var isMember = await accessControl.CheckSpaceMembership(spaceId, userId, ct);
        if (!isMember && space.AccessPolicy > RessourceAccessPolicy.Shared) return Forbid();

        return await Forward($"{RadicaleBase}/{spaceId}/contacts/", spaceId, ct);
    }

    // -------------------------------------------------------------------------
    // REPORT /{spaceId}/ — membership check → forward to Radicale
    // -------------------------------------------------------------------------

    [AcceptVerbs("REPORT")]
    [Route("{spaceId:guid}")]
    public async Task<IActionResult> Report(Guid spaceId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await accessControl.CheckSpaceMembership(spaceId, userId, ct)) return Forbid();
        return await Forward($"{RadicaleBase}/{spaceId}/contacts/", spaceId, ct);
    }

    // -------------------------------------------------------------------------
    // GET /{spaceId}/{contactId}.vcf — cache access check → forward
    // -------------------------------------------------------------------------

    [HttpGet("{spaceId:guid}/{contactSegment}.vcf")]
    public async Task<IActionResult> GetContact(Guid spaceId, string contactSegment, CancellationToken ct)
    {
        if (!Guid.TryParse(contactSegment, out var contactId)) return NotFound();
        var cache = await db.ContactCache
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.Id == contactId, ct);
        if (cache == null) return NotFound();
        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Read, User)) return Forbid();
        return await Forward($"{RadicaleBase}/{spaceId}/contacts/{contactId}.vcf", spaceId, ct);
    }

    // -------------------------------------------------------------------------
    // PUT /{spaceId}/{contactId}.vcf — membership check → forward → cache upsert
    // -------------------------------------------------------------------------

    [HttpPut("{spaceId:guid}/{contactSegment}.vcf")]
    public async Task<IActionResult> PutContact(Guid spaceId, string contactSegment, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await accessControl.CheckSpaceMembership(spaceId, userId, ct)) return Forbid();
        if (!Guid.TryParse(contactSegment, out var contactId)) return BadRequest("Contact ID must be a valid GUID.");

        var res = await ForwardRaw($"{RadicaleBase}/{spaceId}/contacts/{contactId}.vcf", ct, spaceId);

        if (res.IsSuccessStatusCode)
        {
            var now = DateTime.UtcNow;
            var etag = res.Headers.ETag?.Tag?.Trim('"');
            var existing = await db.ContactCache
                .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.Id == contactId, ct);
            if (existing == null)
                db.ContactCache.Add(new ContactCache
                    { Id = contactId, SpaceId = spaceId, OwnerUserId = userId, ETag = etag, LastModified = now });
            else
                (existing.ETag, existing.LastModified) = (etag, now);
            await db.SaveChangesAsync(ct);
            if (etag != null) Response.Headers.ETag = $"\"{etag}\"";
        }

        Response.StatusCode = (int)res.StatusCode;
        return new EmptyResult();
    }

    // -------------------------------------------------------------------------
    // DELETE /{spaceId}/{contactId}.vcf — ownership check → forward → cache delete
    // -------------------------------------------------------------------------

    [HttpDelete("{spaceId:guid}/{contactSegment}.vcf")]
    public async Task<IActionResult> DeleteContact(Guid spaceId, string contactSegment, CancellationToken ct)
    {
        if (!Guid.TryParse(contactSegment, out var contactId)) return NotFound();
        var cache = await db.ContactCache
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.Id == contactId, ct);
        if (cache == null) return NotFound();
        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Write, User)) return Forbid();

        var res = await ForwardRaw($"{RadicaleBase}/{spaceId}/contacts/{contactId}.vcf", ct, spaceId);
        if (res.IsSuccessStatusCode) { db.ContactCache.Remove(cache); await db.SaveChangesAsync(ct); }

        Response.StatusCode = (int)res.StatusCode;
        return new EmptyResult();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<IActionResult> Forward(string url, Guid? spaceId, CancellationToken ct)
    {
        var res = await ForwardRaw(url, ct, spaceId);
        var contentType = res.Content.Headers.ContentType?.ToString() ?? "application/xml; charset=utf-8";
        var body = await res.Content.ReadAsStringAsync(ct);

        if (contentType.Contains("xml") && spaceId.HasValue)
        {
            body = body.Replace($"/{spaceId}/calendar/", $"/dav/caldav/{spaceId}/");
            body = body.Replace($"/{spaceId}/contacts/", $"/dav/carddav/{spaceId}/");
        }

        var result = Content(body, contentType);
        result.StatusCode = (int)res.StatusCode;
        return result;
    }

    private async Task<HttpResponseMessage> ForwardRaw(string url, CancellationToken ct, Guid? spaceId = null)
    {
        var req = new HttpRequestMessage(new HttpMethod(Request.Method), url);
        foreach (var h in new[] { "Depth", "If-Match", "If-None-Match" })
            if (Request.Headers.TryGetValue(h, out var v))
                req.Headers.TryAddWithoutValidation(h, v.ToString());
        if (spaceId.HasValue)
            req.Headers.TryAddWithoutValidation("X-Remote-User", spaceId.Value.ToString());

        if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            Request.EnableBuffering();
            var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms, ct);
            ms.Position = 0;
            req.Content = new StreamContent(ms);
            if (Request.ContentType != null)
                req.Content.Headers.TryAddWithoutValidation("Content-Type", Request.ContentType);
        }

        return await RadicaleHttp.SendAsync(req, ct);
    }

    private IActionResult MultiStatus(string body)
    {
        var xml = $"""<?xml version="1.0" encoding="utf-8"?><multistatus xmlns="DAV:" xmlns:CR="urn:ietf:params:xml:ns:carddav">{body}</multistatus>""";
        var result = Content(xml, "application/xml; charset=utf-8");
        result.StatusCode = 207;
        return result;
    }

    private static string X(string value) => WebUtility.HtmlEncode(value);
}
