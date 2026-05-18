using FinLens.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace FinLens.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<DynamicHub> _hubContext;

    public NotificationService(IHubContext<DynamicHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToUserAsync(Guid userId, string type, object payload, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group($"user:{userId}")
            .SendAsync("notification", new { type, payload }, cancellationToken);
    }

    public async Task SendToWorkspaceAsync(Guid workspaceId, string type, object payload, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group($"workspace:{workspaceId}")
            .SendAsync("notification", new { type, payload }, cancellationToken);
    }
}

public class DynamicHub : Hub { }