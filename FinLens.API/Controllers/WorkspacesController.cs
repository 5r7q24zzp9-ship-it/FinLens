using FinLens.Application.Common.Interfaces;
using FinLens.Domain.Entities;
using FinLens.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinLens.API.Controllers;

[ApiController]
[Route("api/v1/workspaces")]
[Authorize]
public class WorkspacesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public WorkspacesController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyWorkspaces(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var workspaces = await _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId && wm.IsActive)
            .Select(wm => new
            {
                wm.Workspace.Id,
                wm.Workspace.Name,
                wm.Workspace.Type,
                wm.Role,
                wm.JoinedAt,
                MemberCount = wm.Workspace.Members.Count(m => m.IsActive)
            })
            .ToListAsync(ct);

        return Ok(workspaces);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var workspace = new Workspace
        {
            Name = request.Name,
            Type = request.Type,
            OwnerId = userId
        };
        _context.Workspaces.Add(workspace);

        if (request.Type == WorkspaceType.Corporate && request.Departments != null)
        {
            foreach (var deptName in request.Departments)
            {
                _context.Departments.Add(new Department
                {
                    WorkspaceId = workspace.Id,
                    Name = deptName
                });
            }
        }

        var member = new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceMemberRole.Owner,
            IsActive = true
        };
        _context.WorkspaceMembers.Add(member);

        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, new
        {
            workspace.Id,
            workspace.Name,
            workspace.Type
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkspace(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var workspace = await _context.Workspaces
            .Where(w => w.Id == id)
            .Select(w => new
            {
                w.Id,
                w.Name,
                w.Type,
                Members = w.Members
                    .Where(m => m.IsActive)
                    .Select(m => new
                    {
                        m.UserId,
                        m.User.FullName,
                        m.User.Email,
                        m.Role,
                        Department = m.Department != null ? m.Department.Name : null
                    }),
                Departments = w.Departments.Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.BudgetLimit
                })
            })
            .FirstOrDefaultAsync(ct);

        if (workspace == null) return NotFound();

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == id && wm.UserId == userId && wm.IsActive, ct);

        if (!isMember) return Forbid();

        return Ok(workspace);
    }

    [HttpPost("{id}/invite")]
    public async Task<IActionResult> InviteMember(Guid id, [FromBody] InviteMemberRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var myMembership = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == id && wm.UserId == userId && wm.IsActive, ct);

        if (myMembership == null) return Forbid();
        if (myMembership.Role != WorkspaceMemberRole.Owner && myMembership.Role != WorkspaceMemberRole.Admin)
            return Forbid();

        var invitedUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (invitedUser == null)
            return NotFound(new { message = "Bu e-posta ile kayıtlı kullanıcı bulunamadı." });

        var alreadyMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == id && wm.UserId == invitedUser.Id, ct);

        if (alreadyMember)
            return Conflict(new { message = "Bu kullanıcı zaten workspace üyesi." });

        var member = new WorkspaceMember
        {
            WorkspaceId = id,
            UserId = invitedUser.Id,
            Role = request.Role,
            DepartmentId = request.DepartmentId,
            IsActive = true
        };
        _context.WorkspaceMembers.Add(member);

        await _context.SaveChangesAsync(ct);

        return Ok(new { message = "Kullanıcı workspace'e eklendi." });
    }
}

public record CreateWorkspaceRequest(
    string Name,
    WorkspaceType Type,
    List<string>? Departments
);

public record InviteMemberRequest(
    string Email,
    WorkspaceMemberRole Role,
    Guid? DepartmentId
);