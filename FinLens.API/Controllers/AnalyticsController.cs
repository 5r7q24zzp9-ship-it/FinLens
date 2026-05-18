using FinLens.Application.Common.Interfaces;
using FinLens.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinLens.API.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AnalyticsController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Genel özet — toplam gelir, gider, net
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid workspaceId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var transactions = await _context.Transactions
            .Where(t => t.WorkspaceId == workspaceId &&
                        t.TransactionDate >= startDate &&
                        t.TransactionDate <= endDate &&
                        t.Status != TransactionStatus.Cancelled)
            .GroupBy(t => t.Type)
            .Select(g => new
            {
                Type = g.Key,
                Total = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .ToListAsync(ct);

        var income = transactions.FirstOrDefault(t => t.Type == TransactionType.Income);
        var expense = transactions.FirstOrDefault(t => t.Type == TransactionType.Expense);

        var totalIncome = income?.Total ?? 0;
        var totalExpense = expense?.Total ?? 0;

        // Geçen ay ile karşılaştırma
        var prevStart = startDate.AddMonths(-1);
        var prevEnd = startDate.AddDays(-1);

        var prevTransactions = await _context.Transactions
            .Where(t => t.WorkspaceId == workspaceId &&
                        t.TransactionDate >= prevStart &&
                        t.TransactionDate <= prevEnd &&
                        t.Status != TransactionStatus.Cancelled)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var prevIncome = prevTransactions.FirstOrDefault(t => t.Type == TransactionType.Income)?.Total ?? 0;
        var prevExpense = prevTransactions.FirstOrDefault(t => t.Type == TransactionType.Expense)?.Total ?? 0;

        return Ok(new
        {
            period = new { year, month },
            totalIncome,
            totalExpense,
            netBalance = totalIncome - totalExpense,
            incomeCount = income?.Count ?? 0,
            expenseCount = expense?.Count ?? 0,
            comparison = new
            {
                incomeChange = prevIncome == 0 ? 0 : Math.Round((totalIncome - prevIncome) / prevIncome * 100, 2),
                expenseChange = prevExpense == 0 ? 0 : Math.Round((totalExpense - prevExpense) / prevExpense * 100, 2)
            }
        });
    }

    // Kategori bazlı dağılım
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategoryBreakdown(
        [FromQuery] Guid workspaceId,
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] TransactionType type = TransactionType.Expense,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId!.Value;

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var categories = await _context.Transactions
            .Where(t => t.WorkspaceId == workspaceId &&
                        t.Type == type &&
                        t.TransactionDate >= startDate &&
                        t.TransactionDate <= endDate &&
                        t.Status != TransactionStatus.Cancelled)
            .GroupBy(t => t.Category)
            .Select(g => new
            {
                category = g.Key,
                total = g.Sum(t => t.Amount),
                count = g.Count()
            })
            .OrderByDescending(g => g.total)
            .ToListAsync(ct);

        var grandTotal = categories.Sum(c => c.total);

        var result = categories.Select(c => new
        {
            c.category,
            c.total,
            c.count,
            percentage = grandTotal == 0 ? 0 : Math.Round(c.total / grandTotal * 100, 2)
        });

        return Ok(result);
    }

    // Aylık trend — son 12 ay
    [HttpGet("trend")]
    public async Task<IActionResult> GetMonthlyTrend(
        [FromQuery] Guid workspaceId,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        var startDate = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-11);

        var transactions = await _context.Transactions
            .Where(t => t.WorkspaceId == workspaceId &&
                        t.TransactionDate >= startDate &&
                        t.Status != TransactionStatus.Cancelled)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Type,
                Total = g.Sum(t => t.Amount)
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync(ct);

        var months = Enumerable.Range(0, 12)
            .Select(i => startDate.AddMonths(i))
            .Select(d => new
            {
                year = d.Year,
                month = d.Month,
                income = transactions
                    .FirstOrDefault(t => t.Year == d.Year && t.Month == d.Month && t.Type == TransactionType.Income)?.Total ?? 0,
                expense = transactions
                    .FirstOrDefault(t => t.Year == d.Year && t.Month == d.Month && t.Type == TransactionType.Expense)?.Total ?? 0
            })
            .Select(d => new
            {
                d.year,
                d.month,
                d.income,
                d.expense,
                net = d.income - d.expense
            });

        return Ok(months);
    }

    // Departman bazlı analiz — sadece corporate workspace
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartmentAnalysis(
        [FromQuery] Guid workspaceId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var membership = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (membership == null) return Forbid();

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace?.Type != WorkspaceType.Corporate)
            return BadRequest(new { message = "Bu analiz sadece kurumsal workspace'lerde kullanılabilir." });

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var departments = await _context.Departments
            .Where(d => d.WorkspaceId == workspaceId)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.BudgetLimit,
                MemberCount = d.Members.Count(m => m.IsActive),
                TotalExpense = _context.Transactions
                    .Where(t => t.WorkspaceId == workspaceId &&
                                t.Type == TransactionType.Expense &&
                                t.TransactionDate >= startDate &&
                                t.TransactionDate <= endDate &&
                                t.Status != TransactionStatus.Cancelled &&
                                d.Members.Select(m => m.UserId).Contains(t.OwnerId))
                    .Sum(t => t.Amount)
            })
            .ToListAsync(ct);

        return Ok(departments.Select(d => new
        {
            d.Id,
            d.Name,
            d.MemberCount,
            d.TotalExpense,
            budgetLimit = d.BudgetLimit,
            budgetUsagePercent = d.BudgetLimit.HasValue && d.BudgetLimit > 0
                ? Math.Round(d.TotalExpense / d.BudgetLimit.Value * 100, 2)
                : (decimal?)null
        }));
    }
}