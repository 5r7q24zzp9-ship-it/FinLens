using FinLens.Application.Common.Interfaces;
using FinLens.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using FinLens.Infrastructure.Persistence;

namespace FinLens.API.GraphQL.Queries;

[ExtendObjectType(OperationTypeNames.Query)]
public class AnalyticsQueries
{
    public async Task<SummaryResult> GetSummaryAsync(
        Guid workspaceId,
        int year,
        int month,
        [Service(ServiceKind.Pooled)]ApplicationDbContext context,
        [Service] ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserId!.Value;

        var isMember = await context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) throw new UnauthorizedAccessException();

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var transactions = await context.Transactions
            .Where(t => t.WorkspaceId == workspaceId &&
                        t.TransactionDate >= startDate &&
                        t.TransactionDate <= endDate &&
                        t.Status != TransactionStatus.Cancelled)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var income = transactions.FirstOrDefault(t => t.Type == TransactionType.Income)?.Total ?? 0;
        var expense = transactions.FirstOrDefault(t => t.Type == TransactionType.Expense)?.Total ?? 0;

        return new SummaryResult(income, expense, income - expense);
    }
}

public record SummaryResult(decimal TotalIncome, decimal TotalExpense, decimal NetBalance);