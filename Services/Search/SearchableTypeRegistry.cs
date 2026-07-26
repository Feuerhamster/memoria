using Memoria.Models.Database;
using Memoria.Models.Response;

namespace Memoria.Services.Search;

/// <summary>
/// One entry per entity type that participates in global full-text search. Used only to keep the
/// FTS index (<c>SearchIndexFts</c>) in sync in <see cref="AppDbContext.SaveChangesAsync(CancellationToken)"/>.
/// Access control for reading search results is handled separately, in
/// <see cref="Controllers.Content.SearchController"/>, by reusing <c>IAccessPolicyHelperService</c>
/// — the same rules used everywhere else in the app, not a second SQL-side copy of them.
/// Adding a new searchable type means adding one entry here.
/// </summary>
public record SearchableTypeDescriptor(
    SearchEntityType Type,
    Type ClrType,
    Func<object, (Guid Id, string Title, string? Body)> Extract
);

public static class SearchableTypeRegistry
{
    public static readonly IReadOnlyList<SearchableTypeDescriptor> All = new List<SearchableTypeDescriptor>
    {
        new(SearchEntityType.Post, typeof(Post), entity =>
        {
            var post = (Post)entity;
            return (post.Id, string.Empty, post.Text ?? string.Empty);
        }),
        new(SearchEntityType.Ticket, typeof(Ticket), entity =>
        {
            var ticket = (Ticket)entity;
            return (ticket.Id, ticket.Title, ticket.Description);
        }),
        new(SearchEntityType.File, typeof(FileMetadata), entity =>
        {
            var file = (FileMetadata)entity;
            return (file.Id, file.FileName, null);
        }),
        new(SearchEntityType.Space, typeof(Space), entity =>
        {
            var space = (Space)entity;
            return (space.Id, space.Name, space.Description);
        }),
        new(SearchEntityType.User, typeof(User), entity =>
        {
            var user = (User)entity;
            return (user.Id, user.Username, user.Nickname);
        })
    };

    public static SearchableTypeDescriptor? ForClrType(Type type) => All.FirstOrDefault(d => d.ClrType == type);
}
