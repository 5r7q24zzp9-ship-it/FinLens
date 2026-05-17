using FinLens.Domain.Common;
using FinLens.Domain.Enums;

namespace FinLens.Domain.Entities;

public class ApprovalRequest : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid? ReviewerId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public ApprovalLevel Level { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public Transaction Transaction { get; set; } = null!;
    public User Requester { get; set; } = null!;
    public User? Reviewer { get; set; }
    public ICollection<ApprovalLog> Logs { get; set; } = [];
}

public class ApprovalLog : BaseEntity
{
    public Guid ApprovalRequestId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }

    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    public User Actor { get; set; } = null!;
}

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Payload { get; set; } = "{}";
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
