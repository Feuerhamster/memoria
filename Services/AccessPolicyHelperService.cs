using System.Security.Claims;
using EFCoreSecondLevelCacheInterceptor;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Memoria.Services;

public interface IAccessPolicyHelperService
{
    public Task<bool> CheckAccessPolicy(RessourceAccessPolicy policy, AccessIntent intention, Guid ressourceOwnerId, ClaimsPrincipal user, Guid? spaceId = null);
    public Task<bool> CheckSpaceAccess(Guid spaceId, Guid userId, CancellationToken ct = default);
}

public class AccessPolicyHelperService(AppDbContext db) : IAccessPolicyHelperService
{
    public async Task<bool> CheckAccessPolicy(RessourceAccessPolicy policy, AccessIntent intention, Guid ressourceOwnerId, ClaimsPrincipal user, Guid? spaceId = null)
    {
        var userId = user.GetUserId();

        // Owner always has full access to their own resources
        if (ressourceOwnerId.Equals(userId))
        {
            return true;
        }

        if (policy == RessourceAccessPolicy.Public)
        {
            // Public: Everyone can read
            return intention == AccessIntent.Read;
        }

        if (policy == RessourceAccessPolicy.Shared)
        {
            // Shared: Authenticated users can read
            if (user.Identity != null && user.Identity.IsAuthenticated)
            {
                return intention == AccessIntent.Read;
            }
            else
            {
                return false;
            }
        }

        if (policy == RessourceAccessPolicy.Members)
        {
            if (spaceId == null) return false;

            return await this.CheckSpaceAccess((Guid)spaceId, userId);
        }

        if (policy == RessourceAccessPolicy.Private)
        {
            return ressourceOwnerId.Equals(userId);
        }

        return false;
    }

    public async Task<bool> CheckSpaceAccess(Guid spaceId, Guid userId, CancellationToken ct = default)
    {
        var space = await db.Spaces
            .Cacheable()
            .AsNoTracking()
            .Select(s => new
            {
                s.Id, s.OwnerUserId, Members = s.Members.Select(m => m.Id).ToList()
            })
            .Where(s => s.Id.Equals(spaceId))
            .FirstOrDefaultAsync(ct);

        if (space == null) return false;

        return space.OwnerUserId.Equals(userId) || space.Members.Exists(m => m.Equals(userId));;
    }
}