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

namespace Memoria.Controllers.Content;

[ApiController]
[Route("/posts")]
[Authorize]
public class PostController(AppDbContext db, IAccessPolicyHelperService accessHelper) : ControllerBase
{
    private IQueryable<Post> WithRelations() => db.Posts
        .Include(p => p.Owner)
        .Include(p => p.Space)
        .Include(p => p.Files)
        .Include(p => p.Ticket)
        .AsSplitQuery();

    [HttpGet("{postId:guid}")]
    public async Task<ActionResult<PostResponse>> GetPost(Guid postId, CancellationToken ct)
    {
        var post = await db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id.Equals(postId), ct);

        if (post == null) return new NotFoundApiException();

        var hasAccess = await accessHelper.CheckAccessPolicy(post, AccessIntent.Read, this.User);
        if (!hasAccess) return new AccessDeniedApiException();

        var withRelations = await this.WithRelations().FirstAsync(p => p.Id.Equals(postId), ct);

        return new PostResponse(withRelations);
    }

    [HttpPost]
    public async Task<ActionResult<PostResponse>> CreatePost(CreatePostRequest body, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        if (body.SpaceId != null)
        {
            var isAllowed = await accessHelper.CheckSpaceMembership(body.SpaceId.Value, user.UserId, ct);
            if (!isAllowed) return new AccessDeniedApiException();
        }

        var newPost = new Post(user.UserId, body);

        db.Posts.Add(newPost);

        var res = await db.SaveChangesAsync(ct);

        if (res <= 0) return new OperationFailedApiException();

        var withRelations = await this.WithRelations().FirstAsync(p => p.Id.Equals(newPost.Id), ct);

        return new PostResponse(withRelations);
    }

    [HttpPatch("{postId:guid}")]
    public async Task<ActionResult<PostResponse>> UpdatePost(Guid postId, UpdatePostRequest updater, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.OwnerUserId == user.UserId, ct);

        if (post == null) return new NotFoundApiException();

        updater.Apply(post);

        var updated = await db.SaveChangesAsync(ct);

        if (updated <= 0) return new OperationFailedApiException();

        var withRelations = await this.WithRelations().FirstAsync(p => p.Id.Equals(postId), ct);

        return new PostResponse(withRelations);
    }

    [HttpDelete("{postId:guid}")]
    public async Task<ActionResult> DeletePost(Guid postId)
    {
        var post = await db.Posts.FindAsync(postId);

        if (post == null)
        {
            return new NotFoundApiException();
        }

        var isAllowed = await accessHelper.CheckAccessPolicy(post, AccessIntent.Write, this.User);

        if (!isAllowed) return new AccessDeniedApiException();

        db.Posts.Remove(post);

        try
        {
            var removed = await db.SaveChangesAsync();
            return removed > 0 ? Ok() : new OperationFailedApiException();
        }
        catch (DbUpdateException)
        {
            return new ConflictApiException("Cannot delete a post that still has a ticket attached to it");
        }
    }
}
