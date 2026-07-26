using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Models.Response;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers.Content;

[ApiController]
[Route("/tickets")]
[Authorize]
public class TicketController(AppDbContext db, IAccessPolicyHelperService accessHelper) : ControllerBase
{
    private IQueryable<Ticket> WithRelations() => db.Tickets
        .Include(t => t.Post).ThenInclude(p => p.Owner)
        .Include(t => t.Post).ThenInclude(p => p.Space)
        .Include(t => t.Assignees)
        .AsSplitQuery();

    [HttpGet("{ticketId:guid}")]
    public async Task<ActionResult<TicketResponse>> GetTicket(Guid ticketId, CancellationToken ct)
    {
        var ticket = await this.WithRelations().FirstOrDefaultAsync(t => t.Id.Equals(ticketId), ct);

        if (ticket == null) return new NotFoundApiException();

        var hasAccess = await accessHelper.CheckAccessPolicy(ticket, AccessIntent.Read, this.User);
        if (!hasAccess) return new AccessDeniedApiException();

        return BuildResponse(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<TicketResponse>> CreateTicket(CreateTicketRequest body, CancellationToken ct)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id.Equals(body.PostId), ct);

        if (post == null) return new NotFoundApiException("Post not found");

        var hasAccess = await accessHelper.CheckAccessPolicy(post, AccessIntent.Write, this.User);
        if (!hasAccess) return new AccessDeniedApiException("No write access to the target post");

        var alreadyHasTicket = await db.Tickets.AnyAsync(t => t.PostId.Equals(post.Id), ct);
        if (alreadyHasTicket) return new ConflictApiException("This post already has a ticket attached to it");

        var assignees = new List<User>();

        if (body.AssigneeIds is { Count: > 0 })
        {
            assignees = await db.Users.Where(u => body.AssigneeIds.Contains(u.Id)).ToListAsync(ct);
        }

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            Post = post,
            Title = body.Title,
            Description = body.Description,
            Status = body.Status,
            Priority = body.Priority,
            DueDate = body.DueDate,
            CreatedAt = DateTime.UtcNow,
            Assignees = assignees
        };

        db.Tickets.Add(ticket);

        var res = await db.SaveChangesAsync(ct);

        if (res <= 0) return new OperationFailedApiException();

        var withRelations = await this.WithRelations().FirstAsync(t => t.Id.Equals(ticket.Id), ct);

        return BuildResponse(withRelations);
    }

    [HttpPatch("{ticketId:guid}")]
    public async Task<ActionResult<TicketResponse>> UpdateTicket(Guid ticketId, UpdateTicketRequest update, CancellationToken ct)
    {
        var ticket = await db.Tickets
            .Include(t => t.Post)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id.Equals(ticketId), ct);

        if (ticket == null) return new NotFoundApiException();

        var hasAccess = await accessHelper.CheckAccessPolicy(ticket, AccessIntent.Write, this.User);
        if (!hasAccess) return new AccessDeniedApiException();

        update.Apply(ticket);

        if (update.AssigneeIds != null)
        {
            ticket.Assignees = await db.Users.Where(u => update.AssigneeIds.Contains(u.Id)).ToListAsync(ct);
        }

        var changed = await db.SaveChangesAsync(ct);

        if (changed <= 0) return new OperationFailedApiException();

        var withRelations = await this.WithRelations().FirstAsync(t => t.Id.Equals(ticketId), ct);

        return BuildResponse(withRelations);
    }

    [HttpDelete("{ticketId:guid}")]
    public async Task<ActionResult> DeleteTicket(Guid ticketId, CancellationToken ct)
    {
        var ticket = await db.Tickets
            .Include(t => t.Post)
            .FirstOrDefaultAsync(t => t.Id.Equals(ticketId), ct);

        if (ticket == null) return new NotFoundApiException();

        var hasAccess = await accessHelper.CheckAccessPolicy(ticket, AccessIntent.Write, this.User);
        if (!hasAccess) return new AccessDeniedApiException();

        db.Tickets.Remove(ticket);

        var removed = await db.SaveChangesAsync(ct);

        return removed > 0 ? Ok() : new OperationFailedApiException();
    }

    private static TicketResponse BuildResponse(Ticket ticket) => new()
    {
        Id = ticket.Id,
        PostId = ticket.PostId,
        Owner = new PublicEmbeddedUser(ticket.Post.Owner),
        Space = ticket.Post.Space != null ? new EmbeddedSpace(ticket.Post.Space.Id, ticket.Post.Space.Name) : null,
        AccessPolicy = ticket.AccessPolicy,
        ContextAvailability = ticket.ContextAvailability,
        CreatedAt = ticket.CreatedAt,
        ModifiedAt = ticket.ModifiedAt,
        Title = ticket.Title,
        Description = ticket.Description,
        Status = ticket.Status,
        Priority = ticket.Priority,
        DueDate = ticket.DueDate,
        SubTasks = ticket.SubTasks,
        Assignees = ticket.Assignees.Select(u => new PublicEmbeddedUser(u)).ToList()
    };
}
