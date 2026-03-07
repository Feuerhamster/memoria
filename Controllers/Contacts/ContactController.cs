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

namespace Memoria.Controllers.Contacts;

[ApiController]
[Route("/api/spaces/{spaceId:guid}/contacts")]
[Authorize]
public class ContactController(
    AppDbContext db,
    IAccessPolicyHelperService accessControl,
    IRadicaleClient radicale) : ControllerBase
{
    // -------------------------------------------------------------------------
    // GET /api/spaces/{spaceId}/contacts — list contacts
    // -------------------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<List<ContactDto>>> ListContacts(Guid spaceId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var isMember = await accessControl.CheckSpaceMembership(spaceId, userId, ct);
        var maxPolicy = isMember ? RessourceAccessPolicy.Members : RessourceAccessPolicy.Shared;

        var cacheEntries = await db.ContactCache
            .Where(c => c.SpaceId == spaceId
                        && (c.AccessPolicy <= maxPolicy || c.OwnerUserId == userId))
            .ToListAsync(ct);

        var result = new List<ContactDto>();
        foreach (var entry in cacheEntries)
        {
            try
            {
                var vcf = await radicale.GetContactVcf(spaceId, entry.Id, ct);
                var dto = ParseVcfToDto(entry, vcf);
                if (dto != null) result.Add(dto);
            }
            catch
            {
                // Skip contacts that can't be retrieved from Radicale
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // GET /api/spaces/{spaceId}/contacts/{id} — get single contact
    // -------------------------------------------------------------------------

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContactDto>> GetContact(Guid spaceId, Guid id, CancellationToken ct)
    {
        var cache = await db.ContactCache
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.Id == id, ct);
        if (cache == null) return new NotFoundApiException();

        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Read, User))
            return new ActionNotAllowedApiException();

        var vcf = await radicale.GetContactVcf(spaceId, id, ct);
        var dto = ParseVcfToDto(cache, vcf);
        return dto != null ? Ok(dto) : new OperationFailedApiException();
    }

    // -------------------------------------------------------------------------
    // POST /api/spaces/{spaceId}/contacts — create contact
    // -------------------------------------------------------------------------

    [HttpPost]
    public async Task<ActionResult<ContactDto>> CreateContact(
        Guid spaceId, CreateContactRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await accessControl.CheckSpaceMembership(spaceId, userId, ct))
            return new ActionNotAllowedApiException();

        var contactId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var vcf = BuildVcf(contactId, request);
        await radicale.PutContactVcf(spaceId, contactId, vcf, ct);

        var cache = new ContactCache
        {
            Id = contactId,
            SpaceId = spaceId,
            OwnerUserId = userId,
            LastModified = now
        };
        db.ContactCache.Add(cache);
        await db.SaveChangesAsync(ct);

        return Created($"/api/spaces/{spaceId}/contacts/{contactId}",
            new ContactDto
            {
                Id = contactId,
                SpaceId = spaceId,
                OwnerUserId = userId,
                FormattedName = request.FormattedName,
                GivenName = request.GivenName,
                FamilyName = request.FamilyName,
                Organization = request.Organization,
                Title = request.Title,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Notes = request.Notes,
                LastModified = now
            });
    }

    // -------------------------------------------------------------------------
    // PATCH /api/spaces/{spaceId}/contacts/{id} — update contact
    // -------------------------------------------------------------------------

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ContactDto>> UpdateContact(
        Guid spaceId, Guid id, UpdateContactRequest request, CancellationToken ct)
    {
        var cache = await db.ContactCache
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.Id == id, ct);
        if (cache == null) return new NotFoundApiException();

        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Write, User))
            return new ActionNotAllowedApiException();

        // Load existing VCF, apply patches, put back
        var existingVcf = await radicale.GetContactVcf(spaceId, id, ct);
        var existingDto = ParseVcfToDto(cache, existingVcf);
        if (existingDto == null) return new OperationFailedApiException();

        var merged = new CreateContactRequest
        {
            FormattedName = request.FormattedName ?? existingDto.FormattedName,
            GivenName = request.GivenName ?? existingDto.GivenName,
            FamilyName = request.FamilyName ?? existingDto.FamilyName,
            Organization = request.Organization ?? existingDto.Organization,
            Title = request.Title ?? existingDto.Title,
            Email = request.Email ?? existingDto.Email,
            PhoneNumber = request.PhoneNumber ?? existingDto.PhoneNumber,
            Notes = request.Notes ?? existingDto.Notes
        };

        var updatedVcf = BuildVcf(id, merged);
        await radicale.PutContactVcf(spaceId, id, updatedVcf, ct);

        cache.LastModified = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var dto = ParseVcfToDto(cache, updatedVcf);
        return dto != null ? Ok(dto) : new OperationFailedApiException();
    }

    // -------------------------------------------------------------------------
    // DELETE /api/spaces/{spaceId}/contacts/{id} — delete contact
    // -------------------------------------------------------------------------

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteContact(Guid spaceId, Guid id, CancellationToken ct)
    {
        var cache = await db.ContactCache
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.Id == id, ct);
        if (cache == null) return new NotFoundApiException();

        if (!await accessControl.CheckAccessPolicy(cache, AccessIntent.Write, User))
            return new ActionNotAllowedApiException();

        await radicale.DeleteContact(spaceId, id, ct);

        db.ContactCache.Remove(cache);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -------------------------------------------------------------------------
    // Private helpers — minimal vCard 3.0 serialization
    // -------------------------------------------------------------------------

    private static string BuildVcf(Guid contactId, CreateContactRequest req)
    {
        var lines = new List<string>
        {
            "BEGIN:VCARD",
            "VERSION:3.0",
            $"UID:{contactId}",
            $"FN:{Escape(req.FormattedName)}"
        };

        if (req.GivenName != null || req.FamilyName != null)
            lines.Add($"N:{Escape(req.FamilyName ?? "")};{Escape(req.GivenName ?? "")};;;");

        if (req.Organization != null)
            lines.Add($"ORG:{Escape(req.Organization)}");

        if (req.Title != null)
            lines.Add($"TITLE:{Escape(req.Title)}");

        if (req.Email != null)
            lines.Add($"EMAIL;TYPE=INTERNET:{Escape(req.Email)}");

        if (req.PhoneNumber != null)
            lines.Add($"TEL;TYPE=VOICE:{Escape(req.PhoneNumber)}");

        if (req.Notes != null)
            lines.Add($"NOTE:{Escape(req.Notes)}");

        lines.Add($"REV:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        lines.Add("END:VCARD");

        return string.Join("\r\n", lines) + "\r\n";
    }

    /// <summary>Parses a minimal vCard 3.0/4.0 string into a ContactDto.</summary>
    private static ContactDto? ParseVcfToDto(ContactCache cache, string vcf)
    {
        try
        {
            var props = vcf.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(':', 2))
                .Where(parts => parts.Length == 2)
                .GroupBy(
                    parts => parts[0].Split(';')[0].ToUpperInvariant(),
                    parts => parts[1].Trim())
                .ToDictionary(g => g.Key, g => g.First());

            var dto = new ContactDto
            {
                Id = cache.Id,
                SpaceId = cache.SpaceId,
                OwnerUserId = cache.OwnerUserId,
                ETag = cache.ETag,
                LastModified = cache.LastModified
            };

            if (props.TryGetValue("FN", out var fn))
                dto.FormattedName = fn;

            if (props.TryGetValue("N", out var n))
            {
                var parts = n.Split(';');
                dto.FamilyName = parts.Length > 0 ? Unescape(parts[0]) : null;
                dto.GivenName = parts.Length > 1 ? Unescape(parts[1]) : null;
            }

            if (props.TryGetValue("ORG", out var org))
                dto.Organization = Unescape(org);

            if (props.TryGetValue("TITLE", out var title))
                dto.Title = Unescape(title);

            if (props.TryGetValue("EMAIL", out var email))
                dto.Email = Unescape(email);

            if (props.TryGetValue("TEL", out var tel))
                dto.PhoneNumber = Unescape(tel);

            if (props.TryGetValue("NOTE", out var note))
                dto.Notes = Unescape(note);

            return dto;
        }
        catch
        {
            return null;
        }
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(",", "\\,").Replace(";", "\\;").Replace("\n", "\\n");

    private static string Unescape(string value) =>
        value.Replace("\\n", "\n").Replace("\\N", "\n")
             .Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");
}
