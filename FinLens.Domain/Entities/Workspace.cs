using FinLens.Domain.Common;
using FinLens.Domain.Enums;

namespace FinLens.Domain.Entities;

public class Workspace : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public WorkspaceType Type { get; set; }
    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<WorkspaceMember> Members { get; set; } = [];
    public ICollection<Department> Departments { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<BudgetLimit> BudgetLimits { get; set; } = [];
    public ICollection<RecurringTransaction> RecurringTransactions { get; set; } = [];
}

public class WorkspaceMember : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public WorkspaceMemberRole Role { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
    public Department? Department { get; set; }
}

public class Department : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? BudgetLimit { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<WorkspaceMember> Members { get; set; } = [];
}
