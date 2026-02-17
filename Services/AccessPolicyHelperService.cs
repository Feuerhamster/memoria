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
    public Task<bool> CheckAccessPolicy(IAccessManagedRessource ressource, AccessIntent intention, ClaimsPrincipal user);
    public Task<bool> CheckSpaceMembership(Guid spaceId, Guid userId, CancellationToken ct = default);
}

public class AccessPolicyHelperService(AppDbContext db) : IAccessPolicyHelperService
{
    public async Task<bool> CheckAccessPolicy(RessourceAccessPolicy policy, AccessIntent intention, Guid ressourceOwnerId, ClaimsPrincipal user, Guid? spaceId = null)
    {
        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        if (intention == AccessIntent.Read && policy == RessourceAccessPolicy.Public)
        {
            return true;
        }

        if (!isAuthenticated)
        {
            return false;
        }

        var userId = user.GetUserId();
        var isOwner = ressourceOwnerId.Equals(userId);

        if (intention == AccessIntent.Read)
        {
            return policy switch
            {
                RessourceAccessPolicy.Shared  => true,
                RessourceAccessPolicy.Members => (spaceId.HasValue && await CheckSpaceMembership(spaceId.Value, userId)) || isOwner,
                RessourceAccessPolicy.Private => isOwner,
                _ => false
            };
        }

        if (intention == AccessIntent.Write)
        {
            if (isOwner) return true;
            
            return policy switch
            {
                RessourceAccessPolicy.Public  => spaceId.HasValue && await CheckSpaceMembership(spaceId.Value, userId),
                RessourceAccessPolicy.Shared  => spaceId.HasValue && await CheckSpaceMembership(spaceId.Value, userId),
                RessourceAccessPolicy.Members => spaceId.HasValue && await CheckSpaceMembership(spaceId.Value, userId),
                RessourceAccessPolicy.Private => isOwner,
                _ => false
            };
        }

        return false;
    }

    public Task<bool> CheckAccessPolicy(IAccessManagedRessource ressource, AccessIntent intention, ClaimsPrincipal user)
    {
        return this.CheckAccessPolicy(ressource.AccessPolicy, intention, ressource.OwnerUserId, user, ressource.SpaceId);
    }

    public async Task<bool> CheckSpaceMembership(Guid spaceId, Guid userId, CancellationToken ct = default)
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