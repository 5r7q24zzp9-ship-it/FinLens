using FinLens.Application.Common.Interfaces;
using FinLens.Domain.Entities;
using FinLens.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinLens.API.Controllers;

[ApiController]
[Route("api/v1/approvals")]
[Authorize]
public class ApprovalsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;

    public ApprovalsController(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        INotificationService notificationService)
    {
        _context = context;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals([FromQuery] Guid workspaceId, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var membership = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (membership == null) return Forbid();

        if (membership.Role != WorkspaceMemberRole.Accountant &&
            membership.Role != WorkspaceMemberRole.Admin &&
            membership.Role != WorkspaceMemberRole.Owner)
            return Forbid();

        var approvals = await _context.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending &&
                        a.Transaction.WorkspaceId == workspaceId)
            .Select(a => new
            {
                a.Id,
                a.Status,
                a.Level,
                a.CreatedAt,
                Transaction = new
                {
                    a.Transaction.Id,
                    a.Transaction.Amount,
                    a.Transaction.Currency,
                    a.Transaction.Category,
                    a.Transaction.Description,
                    a.Transaction.TransactionDate
                },
                Requester = new
                {
                    a.Requester.FullName,
                    a.Requester.Email
                }
            })
            .ToListAsync(ct);

        return Ok(approvals);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var approval = await _context.ApprovalRequests
            .Include(a => a.Transaction)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (approval == null) return NotFound();

        var membership = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == approval.Transaction.WorkspaceId &&
                                       wm.UserId == userId && wm.IsActive, ct);

        if (membership == null) return Forbid();

        if (membership.Role != WorkspaceMemberRole.Accountant &&
            membership.Role != WorkspaceMemberRole.Admin &&
            membership.Role != WorkspaceMemberRole.Owner)
            return Forbid();

        approval.Status = ApprovalStatus.Approved;
        approval.ReviewerId = userId;
        approval.ReviewNote = request.Note;
        approval.ReviewedAt = DateTime.UtcNow;
        approval.Transaction.Status = TransactionStatus.Approved;

        _context.ApprovalLogs.Add(new ApprovalLog
        {
            ApprovalRequestId = approval.Id,
            ActorId = userId,
            Action = "approved",
            Note = request.Note
        });

        await _context.SaveChangesAsync(ct);

        await _notificationService.SendToUserAsync(
            approval.RequesterId,
            "approval.approved",
            new
            {
                transactionId = approval.TransactionId,
                amount = approval.Transaction.Amount,
                message = "Harcama talebiniz onaylandı."
            },
            ct);

        return Ok(new { message = "Talep onaylandı." });
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var approval = await _context.ApprovalRequests
            .Include(a => a.Transaction)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (approval == null) return NotFound();

        var membership = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == approval.Transaction.WorkspaceId &&
                                       wm.UserId == userId && wm.IsActive, ct);

        if (membership == null) return Forbid();

        approval.Status = ApprovalStatus.Rejected;
        approval.ReviewerId = userId;
        approval.ReviewNote = request.Note;
        approval.ReviewedAt = DateTime.UtcNow;
        approval.Transaction.Status = TransactionStatus.Rejected;

        _context.ApprovalLogs.Add(new ApprovalLog
        {
            ApprovalRequestId = approval.Id,
            ActorId = userId,
            Action = "rejected",
            Note = request.Note
        });

        await _context.SaveChangesAsync(ct);

        await _notificationService.SendToUserAsync(
            approval.RequesterId,
            "approval.rejected",
            new
            {
                transactionId = approval.TransactionId,
                amount = approval.Transaction.Amount,
                message = "Harcama talebiniz reddedildi.",
                note = request.Note
            },
            ct);

        return Ok(new { message = "Talep reddedildi." });
    }
}

public record ReviewRequest(string? Note);