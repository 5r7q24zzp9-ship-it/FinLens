using FinLens.Application.Common.Interfaces;
using FinLens.Domain.Entities;
using FinLens.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinLens.API.Controllers;

[ApiController]
[Route("api/v1/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TransactionsController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] Guid workspaceId,
        [FromQuery] TransactionType? type,
        [FromQuery] string? category,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId!.Value;

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        var query = _context.Transactions
            .Where(t => t.WorkspaceId == workspaceId);

        if (type.HasValue) query = query.Where(t => t.Type == type.Value);
        if (!string.IsNullOrEmpty(category)) query = query.Where(t => t.Category == category);
        if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value);

        var total = await query.CountAsync(ct);

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Type,
                t.Amount,
                t.Currency,
                t.Category,
                t.Description,
                t.Status,
                t.TransactionDate,
                t.CreatedAt,
                Owner = new { t.Owner.FullName, t.Owner.Email },
                DocumentCount = t.Documents.Count,
                SplitCount = t.Splits.Count
            })
            .ToListAsync(ct);

        return Ok(new
        {
            total,
            page,
            pageSize,
            data = transactions
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction(
        [FromBody] CreateTransactionRequest request,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == request.WorkspaceId && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        var transaction = new Transaction
        {
            OwnerId = userId,
            WorkspaceId = request.WorkspaceId,
            Type = request.Type,
            Amount = request.Amount,
            Currency = request.Currency ?? "TRY",
            Category = request.Category,
            Description = request.Description,
            TransactionDate = request.TransactionDate,
            Status = TransactionStatus.Active
        };

        _context.Transactions.Add(transaction);

        // Split mantığı — birden fazla workspace'e böl
        if (request.Splits != null && request.Splits.Any())
        {
            var splitTotal = request.Splits.Sum(s => s.Amount);
            if (splitTotal != request.Amount)
                return BadRequest(new { message = "Split tutarları toplamı işlem tutarına eşit olmalı." });

            foreach (var split in request.Splits)
            {
                var splitMember = await _context.WorkspaceMembers
                    .AnyAsync(wm => wm.WorkspaceId == split.WorkspaceId && wm.UserId == userId && wm.IsActive, ct);

                if (!splitMember)
                    return Forbid();

                _context.TransactionSplits.Add(new TransactionSplit
                {
                    TransactionId = transaction.Id,
                    WorkspaceId = split.WorkspaceId,
                    Amount = split.Amount,
                    Status = TransactionStatus.Active
                });
            }
        }

        // Bütçe limit kontrolü
        var budgetLimit = await _context.BudgetLimits
            .FirstOrDefaultAsync(b =>
                b.WorkspaceId == request.WorkspaceId &&
                b.Category == request.Category &&
                b.Period == BudgetPeriod.Monthly, ct);

        if (budgetLimit != null && request.Type == TransactionType.Expense)
        {
            var startOfMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var spent = await _context.Transactions
                .Where(t =>
                    t.WorkspaceId == request.WorkspaceId &&
                    t.Category == request.Category &&
                    t.Type == TransactionType.Expense &&
                    t.TransactionDate >= startOfMonth)
                .SumAsync(t => t.Amount, ct);

            if (spent + request.Amount > budgetLimit.LimitAmount)
            {
                _context.BudgetAlerts.Add(new BudgetAlert
                {
                    BudgetLimitId = budgetLimit.Id,
                    WorkspaceId = request.WorkspaceId,
                    TriggeredAtAmount = spent + request.Amount,
                    Threshold = "exceeded"
                });
            }
            else if ((spent + request.Amount) / budgetLimit.LimitAmount >= 0.8m)
            {
                _context.BudgetAlerts.Add(new BudgetAlert
                {
                    BudgetLimitId = budgetLimit.Id,
                    WorkspaceId = request.WorkspaceId,
                    TriggeredAtAmount = spent + request.Amount,
                    Threshold = "warning"
                });
            }
        }

        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, new
        {
            transaction.Id,
            transaction.Type,
            transaction.Amount,
            transaction.Currency,
            transaction.Category,
            transaction.TransactionDate
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransaction(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var transaction = await _context.Transactions
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Type,
                t.Amount,
                t.Currency,
                t.Category,
                t.Description,
                t.Status,
                t.TransactionDate,
                t.CreatedAt,
                Owner = new { t.Owner.FullName, t.Owner.Email },
                Splits = t.Splits.Select(s => new
                {
                    s.WorkspaceId,
                    s.Amount,
                    s.Status
                }),
                Documents = t.Documents.Select(d => new
                {
                    d.Id,
                    d.FileUrl,
                    d.FileType,
                    d.UploadedAt
                })
            })
            .FirstOrDefaultAsync(ct);

        if (transaction == null) return NotFound();

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == _context.Transactions
                .Where(t => t.Id == id)
                .Select(t => t.WorkspaceId)
                .FirstOrDefault() && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        return Ok(transaction);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId, ct);

        if (transaction == null) return NotFound();

        transaction.Status = TransactionStatus.Cancelled;
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}

public record CreateTransactionRequest(
    Guid WorkspaceId,
    TransactionType Type,
    decimal Amount,
    string? Currency,
    string Category,
    string? Description,
    DateOnly TransactionDate,
    List<SplitRequest>? Splits
);

public record SplitRequest(
    Guid WorkspaceId,
    decimal Amount
);