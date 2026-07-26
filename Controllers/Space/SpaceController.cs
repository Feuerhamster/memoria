using EFCoreSecondLevelCacheInterceptor;
using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Models.Response;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers;

[ApiController]
[Route("/spaces")]
[Authorize]
public class SpaceController(AppDbContext database, ISpaceService spaceService, IAccessPolicyHelperService accessHelper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Space>>> GetAllSpacesForUser()
    {
        var user = this.User.GetAuthClaimsData();

        var res = await database.Spaces.Cacheable().AsNoTracking()
            .Where(
                s => s.AccessPolicy <= RessourceAccessPolicy.Shared || (s.OwnerUserId.Equals(user.UserId) || s.Members.Any(m => m.Id.Equals(user.UserId)))
                )
            .ToListAsync();

        return res;
    }

    [HttpPost]
    public async Task<ActionResult<Space>> CreateSpace(SpaceCreateRequest spaceCreate)
    {
        var user = this.User.GetAuthClaimsData();

        var newSpace = new Space(spaceCreate.Name, spaceCreate.Description, user.UserId);

        database.Spaces.Add(newSpace);
        var res = await database.SaveChangesAsync();

        if (res > 0)
        {
            return newSpace;
        }
        else
        {
            return new OperationFailedApiException();
        }
    }

    [HttpPatch("{spaceId:guid}")]
    public async Task<ActionResult<Space>> UpdateSpace(Guid spaceId, SpaceUpdateRequest update, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        var space = await spaceService.GetSpace(spaceId, ct);

        if (space == null) return new NotFoundApiException();

        if (!space.OwnerUserId.Equals(user.UserId))
        {
            return new ActionNotAllowedApiException();
        }

        update.Apply(space);

        var res = await database.SaveChangesAsync(ct);
        return res > 0  ? Ok(space) : new OperationFailedApiException();
    }

    [HttpDelete("{spaceId:guid}")]
    public async Task<ActionResult<Space>> DeleteSpace(Guid spaceId)
    {
        var user = this.User.GetAuthClaimsData();

        var space = await database.Spaces.FindAsync(spaceId);

        if (space == null)
        {
            return new NotFoundApiException();
        }

        if (!space.OwnerUserId.Equals(user.UserId))
        {
            return new ActionNotAllowedApiException();
        }

        database.Spaces.Remove(space);

        var res = await database.SaveChangesAsync();

        if (res > 0)
        {
            return Ok();
        }
        else
        {
            return new OperationFailedApiException();
        }
    }

    [HttpGet("{spaceId:guid}/members")]
    public async Task<ActionResult<List<PublicEmbeddedUser>>> GetSpaceMembers(Guid spaceId, CancellationToken ct)
    {
        var members = await database.Spaces
            .Where(s => s.Id.Equals(spaceId))
            .Select(s => s.Members).FirstOrDefaultAsync(ct);

        if (members == null) return new NotFoundApiException();

        return members.Select(member => new PublicEmbeddedUser(member)).ToList();
    }

    /// <summary>
    /// Adds a space member. Adding yourself is a self-join and still follows AllowJoins/AccessPolicy
    /// (unchanged behavior); the owner can add anyone else directly, bypassing those rules — the
    /// only way to get someone into a Private/Members space, which never allows self-join.
    /// </summary>
    [HttpPost("{spaceId:guid}/members/{userId:guid}")]
    public async Task<ActionResult> AddSpaceMember(Guid spaceId, Guid userId, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        var space = await spaceService.GetSpace(spaceId, ct);

        if (space == null) return new NotFoundApiException();

        var isSelf = userId.Equals(user.UserId);
        var isOwner = space.OwnerUserId.Equals(user.UserId);

        if (!isOwner)
        {
            if (!isSelf) return new ActionNotAllowedApiException();
            if (!space.AllowJoins || space.AccessPolicy > RessourceAccessPolicy.Shared) return new ActionNotAllowedApiException();
        }

        var userToAdd = await database.Users.FirstOrDefaultAsync(u => u.Id.Equals(userId), ct);

        if (userToAdd == null) return new NotFoundApiException();

        if (!space.Members.Any(m => m.Id.Equals(userId)))
        {
            space.Members.Add(userToAdd);
        }

        var changed = await database.SaveChangesAsync(ct);

        return changed > 0 ? Ok() : new OperationFailedApiException();
    }

    [HttpDelete("{spaceId:guid}/members/{memberId:guid}")]
    public async Task<ActionResult> LeaveSpace(Guid spaceId, Guid memberId, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        var space = await spaceService.GetSpace(spaceId, ct);

        if (space == null) return new NotFoundApiException();

        var userToRemove = await database.Users.FirstOrDefaultAsync(u => u.Id.Equals(memberId), ct);

        if (userToRemove == null) return new NotFoundApiException();

        var isSelfRemoval = memberId.Equals(user.UserId);
        var isOwner = space.OwnerUserId.Equals(user.UserId);

        if (!isOwner && !isSelfRemoval)
        {
            return new ActionNotAllowedApiException();
        }

        space.Members.Remove(userToRemove);

        var changed = await database.SaveChangesAsync(ct);

        return changed > 0 ? Ok() : new OperationFailedApiException();
    }

    /// <summary>
    /// The space's Confluence-like documentation tree — deliberately minimal (id + short excerpt),
    /// purely for navigation. The client fetches the full post (<c>GET /posts/{id}</c>) once the
    /// user actually navigates to a node.
    /// </summary>
    [HttpGet("{spaceId:guid}/documentation")]
    public async Task<ActionResult<List<DocumentTreeNode>>> GetSpaceDocumentation(Guid spaceId, CancellationToken ct)
    {
        var space = await spaceService.GetSpace(spaceId, ct);

        if (space == null) return new NotFoundApiException();

        var hasAccess = await accessHelper.CheckAccessPolicy(space, AccessIntent.Read, this.User);
        if (!hasAccess) return new AccessDeniedApiException();

        var candidates = await database.Posts
            .Where(p => p.SpaceId.Equals(spaceId) && p.IsSpaceDocument)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var visible = new List<Post>();

        foreach (var post in candidates)
        {
            if (await accessHelper.CheckAccessPolicy(post, AccessIntent.Read, this.User))
            {
                visible.Add(post);
            }
        }

        return BuildDocumentTree(visible);
    }

    private static List<DocumentTreeNode> BuildDocumentTree(List<Post> posts)
    {
        var visibleIds = posts.Select(p => p.Id).ToHashSet();
        var nodesById = posts.ToDictionary(p => p.Id, p => new DocumentTreeNode
        {
            Id = p.Id,
            Excerpt = string.IsNullOrEmpty(p.Text) ? null : p.Text[..Math.Min(80, p.Text.Length)]
        });

        var roots = new List<DocumentTreeNode>();

        foreach (var post in posts)
        {
            var node = nodesById[post.Id];

            if (post.ParentId != null && visibleIds.Contains(post.ParentId.Value))
            {
                nodesById[post.ParentId.Value].Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}
