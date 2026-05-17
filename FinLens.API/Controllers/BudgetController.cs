using FinLens.Application.Common.Interfaces;
using FinLens.Domain.Entities;
using FinLens.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinLens.API.Controllers;

[ApiController]
[Route("api/v1/budgets")]
[Authorize]
public class BudgetController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public BudgetController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetBudgets([FromQuery] Guid workspaceId, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        var budgets = await _context.BudgetLimits
            .Where(b => b.WorkspaceId == workspaceId)
            .Select(b => new
            {
                b.Id,
                b.Category,
                b.LimitAmount,
                b.Period,
                b.DepartmentId,
                AlertCount = b.Alerts.Count
            })
            .ToListAsync(ct);

        return Ok(budgets);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var membership = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == request.WorkspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (membership == null) return Forbid();

        if (membership.Role != WorkspaceMemberRole.Owner && membership.Role != WorkspaceMemberRole.Admin)
            return Forbid();

        var existing = await _context.BudgetLimits
            .AnyAsync(b => b.WorkspaceId == request.WorkspaceId &&
                           b.Category == request.Category &&
                           b.Period == request.Period, ct);

        if (existing)
            return Conflict(new { message = "Bu kategori ve dönem için zaten bir limit tanımlı." });

        var budget = new BudgetLimit
        {
            WorkspaceId = request.WorkspaceId,
            Category = request.Category,
            LimitAmount = request.LimitAmount,
            Period = request.Period,
            DepartmentId = request.DepartmentId
        };

        _context.BudgetLimits.Add(budget);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetBudgets), new { workspaceId = request.WorkspaceId }, new
        {
            budget.Id,
            budget.Category,
            budget.LimitAmount,
            budget.Period
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBudget(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var budget = await _context.BudgetLimits
            .Include(b => b.Workspace)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (budget == null) return NotFound();

        var membership = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == budget.WorkspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (membership == null || (membership.Role != WorkspaceMemberRole.Owner && membership.Role != WorkspaceMemberRole.Admin))
            return Forbid();

        _context.BudgetLimits.Remove(budget);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] Guid workspaceId, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        var alerts = await _context.BudgetAlerts
            .Where(a => a.WorkspaceId == workspaceId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.Threshold,
                a.TriggeredAtAmount,
                a.CreatedAt,
                Category = a.BudgetLimit.Category,
                LimitAmount = a.BudgetLimit.LimitAmount
            })
            .ToListAsync(ct);

        return Ok(alerts);
    }
}

public record CreateBudgetRequest(
    Guid WorkspaceId,
    string Category,
    decimal LimitAmount,
    BudgetPeriod Period,
    Guid? DepartmentId
);