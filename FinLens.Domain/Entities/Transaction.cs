using FinLens.Domain.Common;
using FinLens.Domain.Enums;

namespace FinLens.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid OwnerId { get; set; }
    public Guid WorkspaceId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Active;
    public DateOnly TransactionDate { get; set; }

    public User Owner { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
    public ICollection<TransactionSplit> Splits { get; set; } = [];
    public ICollection<Document> Documents { get; set; } = [];
    public ApprovalRequest? ApprovalRequest { get; set; }
}

public class TransactionSplit : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Guid WorkspaceId { get; set; }
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    public Transaction Transaction { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
}

public class Document : BaseEntity
{
    public Guid TransactionId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Transaction Transaction { get; set; } = null!;
}

public class BudgetLimit : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal LimitAmount { get; set; }
    public BudgetPeriod Period { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<BudgetAlert> Alerts { get; set; } = [];
}

public class BudgetAlert : BaseEntity
{
    public Guid BudgetLimitId { get; set; }
    public Guid WorkspaceId { get; set; }
    public decimal TriggeredAtAmount { get; set; }
    public string Threshold { get; set; } = string.Empty;

    public BudgetLimit BudgetLimit { get; set; } = null!;
}

public class RecurringTransaction : BaseEntity
{
    public Guid OwnerId { get; set; }
    public Guid WorkspaceId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RecurringFrequency Frequency { get; set; }
    public DateOnly NextDueDate { get; set; }
    public bool IsActive { get; set; } = true;

    public User Owner { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
}
