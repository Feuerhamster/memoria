using System.Data;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Response;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers.Content;

[ApiController]
[Route("/search")]
[Authorize]
public class SearchController(AppDbContext db, IAccessPolicyHelperService accessHelper) : ControllerBase
{
    private const int DEFAULT_PAGE_SIZE = 20;
    private const int MAX_PAGE_SIZE = 100;

    /// <summary>
    /// How many extra raw matches to fetch beyond the requested page size, since some will get
    /// filtered out afterward by the access check. Pagination past the first page is therefore
    /// approximate once filtering kicks in — an accepted trade-off for simpler code.
    /// </summary>
    private const int OVER_FETCH_FACTOR = 3;

    private record RawHit(SearchEntityType Type, Guid EntityId, string Snippet);

    [HttpGet]
    public async Task<ActionResult<List<SearchResultResponse>>> Search(
        string q,
        int pageSize = DEFAULT_PAGE_SIZE,
        int offset = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return new List<SearchResultResponse>();
        }

        var clampedPageSize = Math.Clamp(pageSize, 1, MAX_PAGE_SIZE);
        var rawHits = await this.RunFtsQuery(q, clampedPageSize * OVER_FETCH_FACTOR, offset, ct);

        var results = new List<SearchResultResponse>();

        foreach (var hit in rawHits)
        {
            var result = await this.TryBuildResult(hit, ct);

            if (result == null)
            {
                continue;
            }

            results.Add(result);

            if (results.Count >= clampedPageSize)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Plain FTS5 match, ordered by relevance — no access filtering here. EF Core can't translate
    /// MATCH/bm25()/snippet(), so this is a hand-written parameterized command instead of LINQ.
    /// Uses the connection the DbContext already owns; must not close/dispose it.
    /// </summary>
    private async Task<List<RawHit>> RunFtsQuery(string rawQuery, int limit, int offset, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT entity_type, entity_id, snippet(SearchIndexFts, -1, '<b>', '</b>', '…', 10) AS snippet
            FROM SearchIndexFts
            WHERE SearchIndexFts MATCH @query
            ORDER BY bm25(SearchIndexFts)
            LIMIT @limit OFFSET @offset
            """;
        command.Parameters.Add(new SqliteParameter("@query", BuildFtsQuery(rawQuery)));
        command.Parameters.Add(new SqliteParameter("@limit", limit));
        command.Parameters.Add(new SqliteParameter("@offset", offset));

        var hits = new List<RawHit>();

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            hits.Add(new RawHit(
                Enum.Parse<SearchEntityType>(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2)
            ));
        }

        return hits;
    }

    /// <summary>
    /// Loads one hit, checks it against <see cref="IAccessPolicyHelperService"/> — the same
    /// authorization rules used everywhere else in the app — and returns null if it's not visible
    /// or no longer exists. Users have no access concept (public profiles, always visible).
    /// </summary>
    private async Task<SearchResultResponse?> TryBuildResult(RawHit hit, CancellationToken ct)
    {
        if (hit.Type == SearchEntityType.User)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id.Equals(hit.EntityId), ct);
            if (user == null) return null;

            return new SearchResultResponse
            {
                EntityType = hit.Type,
                EntityId = hit.EntityId,
                Snippet = hit.Snippet,
                Owner = new PublicEmbeddedUser(user)
            };
        }

        var ressource = await this.LoadRessource(hit.Type, hit.EntityId, ct);

        if (ressource == null) return null;

        var visible = await accessHelper.CheckAccessPolicy(ressource, AccessIntent.Read, this.User);

        if (!visible) return null;

        var owner = await db.Users.FirstOrDefaultAsync(u => u.Id.Equals(ressource.OwnerUserId), ct);
        var space = ressource.SpaceId != null
            ? await db.Spaces.FirstOrDefaultAsync(s => s.Id.Equals(ressource.SpaceId), ct)
            : null;

        return new SearchResultResponse
        {
            EntityType = hit.Type,
            EntityId = hit.EntityId,
            Snippet = hit.Snippet,
            Owner = owner != null ? new PublicEmbeddedUser(owner) : null,
            Space = space != null ? new EmbeddedSpace(space.Id, space.Name) : null
        };
    }

    /// <summary>Only picks which DbSet to load from — Ticket additionally loads its Post, since its OwnerUserId/AccessPolicy/SpaceId are computed from it.</summary>
    private Task<IAccessManagedRessource?> LoadRessource(SearchEntityType type, Guid id, CancellationToken ct) => type switch
    {
        SearchEntityType.Post => FirstOrDefaultAs<Post>(db.Posts.Where(p => p.Id.Equals(id)), ct),
        SearchEntityType.Ticket => FirstOrDefaultAs<Ticket>(db.Tickets.Include(t => t.Post).Where(t => t.Id.Equals(id)), ct),
        SearchEntityType.File => FirstOrDefaultAs<FileMetadata>(db.Files.Where(f => f.Id.Equals(id)), ct),
        SearchEntityType.Space => FirstOrDefaultAs<Space>(db.Spaces.Where(s => s.Id.Equals(id)), ct),
        _ => Task.FromResult<IAccessManagedRessource?>(null)
    };

    private static async Task<IAccessManagedRessource?> FirstOrDefaultAs<T>(IQueryable<T> query, CancellationToken ct) where T : class, IAccessManagedRessource
        => await query.FirstOrDefaultAsync(ct);

    /// <summary>Turns free-text input into an FTS5 MATCH expression: each word becomes a quoted, prefix-matched phrase, ANDed together.</summary>
    private static string BuildFtsQuery(string raw)
    {
        var terms = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var escaped = terms.Select(t => $"\"{t.Replace("\"", "\"\"")}\"*");
        return string.Join(" ", escaped);
    }
}
