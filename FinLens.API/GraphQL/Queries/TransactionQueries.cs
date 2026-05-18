using FinLens.Application.Common.Interfaces;
using FinLens.Domain.Entities;
using FinLens.Infrastructure.Persistence;

namespace FinLens.API.GraphQL.Queries;

[ExtendObjectType(OperationTypeNames.Query)]
public class TransactionQueries
{
    [UseDbContext(typeof(ApplicationDbContext))]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Transaction> GetTransactions(
        [Service(ServiceKind.Pooled)] ApplicationDbContext context,
        [Service] ICurrentUserService currentUser,
        Guid workspaceId)
    {
        var userId = currentUser.UserId!.Value;
        return context.Transactions
            .Where(t => t.WorkspaceId == workspaceId &&
                context.WorkspaceMembers.Any(wm =>
                    wm.WorkspaceId == workspaceId &&
                    wm.UserId == userId &&
                    wm.IsActive));
    }
}