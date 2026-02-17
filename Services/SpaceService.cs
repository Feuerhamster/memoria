using EFCoreSecondLevelCacheInterceptor;
using Memoria.Models;
using Memoria.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Services;

public interface ISpaceService
{
    public Task<List<Space>> GetMemberSpaces(Guid userId);
    public Task<List<Space>> GetPublicSpaces(RessourceAccessPolicy policy);
    public Task<bool> SpaceExists(Guid spaceId);
    public Task<Space?> GetSpace(Guid spaceId, CancellationToken ct);
}

public class SpaceService(AppDbContext db) : ISpaceService
{
    public Task<List<Space>> GetMemberSpaces(Guid userId)
    {
        return db.Spaces.Cacheable().AsNoTracking()
            .Where(s => s.OwnerUserId == userId || s.Members.Any(m => m.Id == userId))
            .ToListAsync();
    }

    public Task<List<Space>> GetPublicSpaces(RessourceAccessPolicy policy)
    {
        return db.Spaces.Cacheable().AsNoTracking()
            .Where(s => s.AccessPolicy <= policy)
            .ToListAsync();
    }

    public Task<bool> SpaceExists(Guid spaceId)
    {
        return db.Spaces.Cacheable().AnyAsync(s => s.Id.Equals(spaceId));
    }

    public Task<Space?> GetSpace(Guid spaceId, CancellationToken ct)
    {
        return db.Spaces.Cacheable().FirstOrDefaultAsync(s => s.Id.Equals(spaceId), cancellationToken: ct);
    }
}