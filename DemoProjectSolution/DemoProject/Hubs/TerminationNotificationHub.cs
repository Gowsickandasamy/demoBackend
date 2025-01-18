using System.Collections.Concurrent;
using DemoProject.Contexts;
using DemoProject.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DemoProject.Hubs
{
    public class TerminationNotificationHub : Hub
    {
        private readonly ILogger<TerminationNotificationHub> _logger;
        private readonly AppDbContext _context;

        private static readonly ConcurrentDictionary<string, string> UserConnections = new();

        public TerminationNotificationHub(AppDbContext context, ILogger<TerminationNotificationHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Log connection details when a client connects
        public override async Task OnConnectedAsync()
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"];
            if (!string.IsNullOrEmpty(sessionId))
            {
                if (Guid.TryParse(sessionId, out Guid parsedSessionId))
                {
                    var session = await _context.UserSessions.FirstOrDefaultAsync(us => us.SessionId == parsedSessionId);
                    if (session != null)
                    {
                        session.ConnectionId = Context.ConnectionId;
                        await _context.SaveChangesAsync();
                        var onlineUsers = await _context.UserSessions

                            .Where(us => us.IsActive)
                            .Select(us => new

                            {
                                us.UserId,
                                us.User.Username,
                                us.User.Role,
                                us.SessionId,
                                us.DeviceInfo,
                                us.ConnectionId
                            })
                            .ToListAsync();

                        await Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers);

                    }
                    await base.OnConnectedAsync();
                }
            }
        }
        // Log disconnection details when a client disconnects
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"];
            if (!string.IsNullOrEmpty(sessionId))
            {
                UserConnections.TryRemove(sessionId, out _);
            }
            return base.OnDisconnectedAsync(exception);
        }

        // Send a notification to a specific connection ID
        public async Task SendNotificationToSession(string sessionId, string message)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Invalid connection ID provided for notification.");
                return;
            }

            if (UserConnections.TryGetValue(sessionId, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("SendMessage", $"Sending Notification to {sessionId}");

            }
        }
    }
}
