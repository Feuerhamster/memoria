using EFCoreSecondLevelCacheInterceptor;
using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers;

[ApiController]
[Route("/spaces")]
[Authorize]
public class SpaceController(AppDbContext database, ISpaceService spaceService) : ControllerBase
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
    
    [HttpPost("{spaceId:guid}/members")]
    public async Task<ActionResult> JoinSpace(Guid spaceId, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        var space = await spaceService.GetSpace(spaceId, ct);

        if (space == null) return new NotFoundApiException();

        if (!space.OwnerUserId.Equals(user.UserId))
        {
            if (!space.AllowJoins || space.AccessPolicy > RessourceAccessPolicy.Shared) return new ActionNotAllowedApiException();
        }
        
        var userToAdd = await database.Users.FirstOrDefaultAsync(u => u.Id.Equals(user.UserId), ct);
        
        if (userToAdd == null) return new OperationFailedApiException();
        
        space.Members.Add(userToAdd);

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

        if (!space.OwnerUserId.Equals(user.UserId) && !space.Members.Any(m => m.Id.Equals(user.UserId)))
        {
            return new ActionNotAllowedApiException();
        }

        space.Members.Remove(userToRemove);
        
        var changed = await database.SaveChangesAsync(ct);
        
        return changed > 0 ? Ok() : new OperationFailedApiException();
    }
}