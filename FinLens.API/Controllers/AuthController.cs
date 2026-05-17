using System.Security.Cryptography;
using System.Text;
using FinLens.Application.Common.Interfaces;
using FinLens.Application.Features.Auth;
using FinLens.Domain.Entities;
using FinLens.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinLens.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public AuthController(IApplicationDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email, ct))
            return Conflict(new { message = "Bu e-posta adresi zaten kullanımda." });

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            IsVerified = false
        };

        _context.Users.Add(user);

        var workspace = new Workspace
        {
            Name = $"{request.FullName} - Kişisel",
            Type = WorkspaceType.Individual,
            OwnerId = user.Id
        };
        _context.Workspaces.Add(workspace);

        var member = new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            Role = WorkspaceMemberRole.Owner,
            IsActive = true
        };
        _context.WorkspaceMembers.Add(member);

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user, "Owner");

        return Ok(new AuthResponse(
            accessToken,
            refreshToken,
            user.FullName,
            user.Email,
            "Owner"
        ));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user == null || user.PasswordHash != HashPassword(request.Password))
            return Unauthorized(new { message = "E-posta veya şifre hatalı." });

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(ct);

        var role = await _context.WorkspaceMembers
            .Where(wm => wm.UserId == user.Id && wm.IsActive)
            .Select(wm => wm.Role.ToString())
            .FirstOrDefaultAsync(ct) ?? "Member";

        var accessToken = _tokenService.GenerateAccessToken(user, role);

        return Ok(new AuthResponse(
            accessToken,
            refreshToken,
            user.FullName,
            user.Email,
            role
        ));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken, ct);

        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            return Unauthorized(new { message = "Geçersiz veya süresi dolmuş refresh token." });

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user, "Member");

        return Ok(new AuthResponse(
            accessToken,
            newRefreshToken,
            user.FullName,
            user.Email,
            "Member"
        ));
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}