using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UserController(AppDbContext db) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicEmbeddedUser>> GetUser(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id.Equals(userId), ct);

        if (user == null) return new NotFoundApiException();
        
        return new PublicEmbeddedUser(user);
    }

    [HttpDelete("me")]
    public async Task<ActionResult> DeleteUser(CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        var deleted = await db.Users.Where(u => u.Id.Equals(user.UserId)).ExecuteDeleteAsync(ct);
        
        if (deleted < 1) return new OperationFailedApiException();
        
        return this.SignOut();
    }
}