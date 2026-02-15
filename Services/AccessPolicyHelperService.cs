using System.Security.Claims;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Services;

public interface IAccessPolicyHelperService
{
    public Task<bool> CheckAccessPolicy(RessourceAccessPolicy policy, RessourceAccessIntention intention, Guid ressourceOwnerId, ClaimsPrincipal user, Guid? spaceId = null);
}

public class AccessPolicyHelperService(AppDbContext db) : IAccessPolicyHelperService
{
    public async Task<bool> CheckAccessPolicy(RessourceAccessPolicy policy, RessourceAccessIntention intention, Guid ressourceOwnerId, ClaimsPrincipal user, Guid? spaceId = null)
    {
        if (policy == RessourceAccessPolicy.Public)
        {
            return intention == RessourceAccessIntention.Read;
        }

        if (policy == RessourceAccessPolicy.GeneralMembers)
        {
            if (user.Identity != null && user.Identity.IsAuthenticated)
            {
                return intention == RessourceAccessIntention.Read;
            }
            else
            {
                return false;
            }
        }

        var claims = user.GetAuthClaimsData();

        if (policy == RessourceAccessPolicy.SpaceMembers)
        {
            if (spaceId == null) return false;
            
            var space = await db.Spaces
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id, s.OwnerUserId, Members = s.Members.Select(m => m.Id).ToList()
                })
                .Where(s => s.Id.Equals(spaceId))
                .FirstOrDefaultAsync();
            
            if (space == null) return false;

            return space.OwnerUserId.Equals(claims.UserId) || space.Members.Exists(m => m.Equals(claims.UserId));
        }

        if (policy == RessourceAccessPolicy.Private)
        {
            return ressourceOwnerId.Equals(claims.UserId);
        }

        return false;
    }
}