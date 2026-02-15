using System.Security.Cryptography;
using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers;

[ApiController]
[Route("/app-access-tokens")]
[Authorize]
public class AppAccessTokensController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserAppAccessToken>>> GetAllTokens()
    {
        var user = this.User.GetAuthClaimsData();

        return await db.AppAccessTokens.Where(t => t.UserId.Equals(user.UserId)).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<AddAppAccessTokenResponse>> CreateAccessToken(CreateAccessTokenRequest body)
    {
        var user = this.User.GetAuthClaimsData();
        
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        var accessToken =  Convert.ToHexString(bytes);

        var newToken = new UserAppAccessToken()
        {
            Name =  body.Name,
            AccessToken = accessToken,
            Permissions =  body.Permissions,
            UserId =  user.UserId
        };
        
        db.AppAccessTokens.Add(newToken);
        
        var inserted = await db.SaveChangesAsync();

        if (inserted > 0)
        {
            return new AddAppAccessTokenResponse()
            {
                AppAccessToken = newToken,
                Secret = accessToken,
            };
        }
        else
        {
            return new OperationFailedApiException();
        }
    }

    [HttpDelete("{accessTokenId:guid}")]
    public async Task<ActionResult> DeleteAccessToken(Guid accessTokenId)
    {
        var user = this.User.GetAuthClaimsData();

        var token = await db.AppAccessTokens.Where(t => t.Id.Equals(accessTokenId) && t.UserId.Equals(user.UserId)).FirstOrDefaultAsync();

        if (token == null)
        {
            return new NotFoundApiException();
        }

        db.AppAccessTokens.Remove(token);
        var deleted = await db.SaveChangesAsync();

        if (deleted > 0)
        {
            return Ok();
        }
        else
        {
            return new OperationFailedApiException();
        }
    }
}