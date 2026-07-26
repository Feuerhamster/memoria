using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Response;
using Memoria.Services;
using Memoria.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers.Content;

[ApiController]
[Route("/timeline")]
[Authorize]
public class TimelineController(AppDbContext db, IAccessPolicyHelperService accessHelper) : ControllerBase
{
    private const int DEFAULT_PAGE_SIZE = 20;
    private const int MAX_PAGE_SIZE = 100;

    /// <summary>Global timeline: everything the current user can see, across their own posts, public/shared posts and spaces they're a member of.</summary>
    [HttpGet]
    public async Task<ActionResult<TimelinePageResponse>> GetGlobalTimeline(string? cursor, int pageSize = DEFAULT_PAGE_SIZE, CancellationToken ct = default)
    {
        var user = this.User.GetAuthClaimsData();

        var memberSpaceIds = await accessHelper.GetMemberSpaceIds(user.UserId, ct);

        var query = db.Posts
            .Where(p => p.ParentId == null && !p.IsArchived)
            .Where(p =>
                p.OwnerUserId.Equals(user.UserId) ||
                p.AccessPolicy == RessourceAccessPolicy.Public ||
                p.AccessPolicy == RessourceAccessPolicy.Shared ||
                (p.AccessPolicy == RessourceAccessPolicy.Members && p.SpaceId != null && memberSpaceIds.Contains(p.SpaceId.Value)));

        return await this.BuildPage(query, cursor, pageSize, ct);
    }

    /// <summary>Timeline scoped to a single space.</summary>
    [HttpGet("{spaceId:guid}")]
    public async Task<ActionResult<TimelinePageResponse>> GetSpaceTimeline(Guid spaceId, string? cursor, int pageSize = DEFAULT_PAGE_SIZE, CancellationToken ct = default)
    {
        var space = await db.Spaces.FirstOrDefaultAsync(s => s.Id.Equals(spaceId), ct);

        if (space == null) return new NotFoundApiException("Space not found");

        var hasAccess = await accessHelper.CheckAccessPolicy(space, AccessIntent.Read, this.User);
        if (!hasAccess) return new AccessDeniedApiException();

        var user = this.User.GetAuthClaimsData();

        var query = db.Posts
            .Where(p => p.SpaceId == spaceId && p.ParentId == null && !p.IsArchived)
            .Where(p => p.AccessPolicy != RessourceAccessPolicy.Private || p.OwnerUserId.Equals(user.UserId));

        return await this.BuildPage(query, cursor, pageSize, ct);
    }

    /// <summary>
    /// Applies keyset pagination (`WHERE CreatedAt &lt; cursor ORDER BY CreatedAt DESC LIMIT n`) instead
    /// of an offset/skip, so page N+1 costs the same as page 1 regardless of how deep the client pages.
    /// Owner/Space/Ticket are loaded as normal single-reference includes (no multiplication risk);
    /// only Files is a collection, so <c>AsSplitQuery()</c> pulls it into its own query instead of
    /// joining and multiplying the Post/Owner/Space columns per file row.
    /// </summary>
    private async Task<TimelinePageResponse> BuildPage(IQueryable<Post> query, string? cursor, int pageSize, CancellationToken ct)
    {
        var clampedPageSize = Math.Clamp(pageSize, 1, MAX_PAGE_SIZE);

        if (PostCursor.TryDecode(cursor, out var parsedCursor))
        {
            query = query.Where(p => p.CreatedAt < parsedCursor.CreatedAt);
        }

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(clampedPageSize + 1)
            .Include(p => p.Owner)
            .Include(p => p.Space)
            .Include(p => p.Files)
            .Include(p => p.Ticket)
            .AsSplitQuery()
            .ToListAsync(ct);

        var hasMore = items.Count > clampedPageSize;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new TimelinePageResponse
        {
            Items = items.Select(p => new PostResponse(p)).ToList(),
            NextCursor = hasMore ? new PostCursor(items[^1].CreatedAt).Encode() : null
        };
    }
}
