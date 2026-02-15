using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoria.Controllers.Content;

[ApiController]
[Route("/posts")]
public class TexNoteController(AppDbContext db, IAccessPolicyHelperService accessHelper) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Post>> CreateTextNotePost(CreateTextNotePostRequest body)
    {
        if (body.SpaceId != null)
        {
            var isAllowed = await accessHelper.CheckAccessPolicy(RessourceAccessPolicy.SpaceMembers, RessourceAccessIntention.Write,
                (Guid)body.SpaceId, this.User, body.SpaceId);

            if (!isAllowed)
            {
                return new AccessDeniedApiException();
            }
        }

        var user = this.User.GetAuthClaimsData();

        var newPost = new Post(user.UserId, body);

        db.Posts.Add(newPost);
        
        var res = await db.SaveChangesAsync();

        if (res > 1)
        {
            return Ok(newPost);
        }
        else
        {
            return new OperationFailedApiException();
        }
    }

    [HttpDelete("{postId:guid}")]
    [Authorize]
    public async Task<ActionResult> DeletePost(Guid postId)
    {
        var post = await db.Posts.FindAsync(postId);

        if (post == null)
        {
            return new NotFoundApiException();
        }

        var isAllowed = await accessHelper.CheckAccessPolicy(post.Visibility, RessourceAccessIntention.Write, post.Id, this.User, post.SpaceId);

        if (!isAllowed)
        {
            return new AccessDeniedApiException();
        }
        
        db.Posts.Remove(post);
        var removed = await db.SaveChangesAsync();

        if (removed > 0)
        {
            return Ok();
        }
        else
        {
            return new OperationFailedApiException();
        }
    }
}