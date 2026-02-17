using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Request;
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
    [HttpPost]
    public async Task<ActionResult<Post>> CreatePost(CreatePostRequest body)
    {
        var user = this.User.GetAuthClaimsData();
        
        if (body.SpaceId != null)
        {
            var isAllowed = await accessHelper.CheckSpaceMembership(body.SpaceId.Value, user.UserId);
            if (!isAllowed) return new AccessDeniedApiException();
        }

        var newPost = new Post(user.UserId, body);

        db.Posts.Add(newPost);
        
        var res = await db.SaveChangesAsync();

        return res > 1 ? Ok(newPost) : new OperationFailedApiException();
    }

    [HttpPatch("{postId:guid}")]
    public async Task<ActionResult<Post>> UpdatePost(Guid postId, UpdatePostRequest updater, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();
        
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.OwnerUserId == user.UserId, ct);
        
        if (post == null) return new NotFoundApiException();
        
        updater.Apply(post);
        
        var updated =  await db.SaveChangesAsync(ct);
        
        return updated > 0 ? post : new OperationFailedApiException();
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
        var removed = await db.SaveChangesAsync();

        return removed > 0 ? Ok() : new OperationFailedApiException();
    }
}