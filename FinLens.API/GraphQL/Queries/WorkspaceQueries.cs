using FinLens.Domain.Entities;
using FinLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinLens.API.GraphQL.Queries;

[ExtendObjectType(OperationTypeNames.Query)]
public class WorkspaceQueries
{
    [UseDbContext(typeof(ApplicationDbContext))]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Workspace> GetWorkspaces(
        [Service(ServiceKind.Pooled)] ApplicationDbContext context,
        ClaimsPrincipal claimsPrincipal)
    {
        var userIdStr = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Enumerable.Empty<Workspace>().AsQueryable();
        var userId = Guid.Parse(userIdStr);

        return context.Workspaces
            .Where(w => w.Members.Any(m => m.UserId == userId && m.IsActive));
    }

    public async Task<Workspace?> GetWorkspaceAsync(
        Guid id,
        [Service(ServiceKind.Pooled)] ApplicationDbContext context,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken ct)
    {
        var userIdStr = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return null;
        var userId = Guid.Parse(userIdStr);

        return await context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == id &&
                w.Members.Any(m => m.UserId == userId && m.IsActive), ct);
    }
}