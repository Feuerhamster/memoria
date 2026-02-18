using Memoria.Attributes;
using Memoria.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoria.Controllers.Dav;

/// <summary>
/// Implements RFC 6764 service discovery via well-known URIs plus a root-level
/// PROPFIND handler so that DAV clients (e.g. GNOME Online Accounts) can discover
/// both WebDAV files and CalDAV calendars from just the server base URL.
/// </summary>
[ApiController]
[Route("dav")]
public class WellKnownController : ControllerBase
{
    // -------------------------------------------------------------------------
    // Well-known redirects (RFC 6764)
    // -------------------------------------------------------------------------

    [AllowAnonymous]
    [AcceptVerbs("GET", "PROPFIND", "OPTIONS")]
    [Route(".well-known/caldav")]
    public IActionResult WellKnownCalDav() => RedirectPermanent("/dav/caldav/");

    [AllowAnonymous]
    [AcceptVerbs("GET", "PROPFIND", "OPTIONS")]
    [Route(".well-known/webdav")]
    public IActionResult WellKnownWebDav() => RedirectPermanent("/dav/webdav/");

    // -------------------------------------------------------------------------
    // Root PROPFIND — used by GNOME Online Accounts and other clients
    // that probe the server base URL to discover available DAV services.
    // -------------------------------------------------------------------------

    [AcceptVerbs("OPTIONS")]
    [AllowAnonymous]
    [Route("")]
    public IActionResult RootOptions()
    {
        Response.Headers["DAV"] = "1, 2, calendar-access";
        Response.Headers.Allow = "OPTIONS, PROPFIND";
        return Ok();
    }

    // PROPFIND on / redirects to /webdav/ so that file managers (GNOME Files etc.)
    // mount the WebDAV file store directly without extra sub-folders.
    // CalDAV discovery happens independently via /.well-known/caldav → /caldav/.
    [AcceptVerbs("PROPFIND")]
    [Authorize(AuthenticationSchemes = BasicAuthHandler.SchemeName, Policy = "CalDav")]
    [EnsureWwwAuthenticate]
    [Route("")]
    public IActionResult RootPropFind() => RedirectPermanent("/dav/webdav/");
}
