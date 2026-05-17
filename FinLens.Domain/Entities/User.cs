using FinLens.Domain.Common;

namespace FinLens.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public bool IsVerified { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
