using FinLens.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinLens.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Workspace> Workspaces { get; }
    DbSet<WorkspaceMember> WorkspaceMembers { get; }
    DbSet<Department> Departments { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<TransactionSplit> TransactionSplits { get; }
    DbSet<Document> Documents { get; }
    DbSet<BudgetLimit> BudgetLimits { get; }
    DbSet<BudgetAlert> BudgetAlerts { get; }
    DbSet<RecurringTransaction> RecurringTransactions { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalLog> ApprovalLogs { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
public interface ITokenService
{
    string GenerateAccessToken(User user, string role);
    string GenerateRefreshToken();
    Guid? ValidateAccessToken(string token);
}
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
public interface INotificationService
{
    Task SendToUserAsync(Guid userId, string type, object payload, CancellationToken cancellationToken = default);
    Task SendToWorkspaceAsync(Guid workspaceId, string type, object payload, CancellationToken cancellationToken = default);
}