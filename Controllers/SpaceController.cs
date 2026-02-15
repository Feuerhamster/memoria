using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers;

[ApiController]
[Route("/spaces")]
public class SpaceController(AppDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Space>>> GetAllSpacesForUser()
    {
        var user = this.User.GetAuthClaimsData();

        var res = await database.Spaces.AsNoTracking()
            .Where(s => s.OwnerUserId.Equals(user.UserId) || s.Members.Any(m => m.Id.Equals(user.UserId)))
            .ToListAsync();

        return res;
    }
    
    [HttpPost]
    [Authorize]
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
    
    [HttpDelete("{spaceId:guid}")]
    [Authorize]
    public async Task<ActionResult<Space>> CreateSpace(Guid spaceId)
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
}