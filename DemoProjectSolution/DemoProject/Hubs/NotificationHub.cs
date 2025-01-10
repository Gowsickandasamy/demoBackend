using System.Collections.Concurrent;
using DemoProject.Contexts;
using DemoProject.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DemoProject.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly AppDbContext _dbContext;

        private static readonly ConcurrentDictionary<int, string> UserConnections = new();

        public NotificationHub(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override Task OnConnectedAsync()
        {
            var userIdString = Context.GetHttpContext()?.Request.Query["userId"];
            if (int.TryParse(userIdString, out int userId))
            {
                UserConnections[userId] = Context.ConnectionId;
            }
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdString = Context.GetHttpContext()?.Request.Query["userId"];
            if (int.TryParse(userIdString, out int userId))
            {
                UserConnections.TryRemove(userId, out _);
            }
            return base.OnDisconnectedAsync(exception);
        }

        public async Task ChangeUserRole(int userId, string newRole)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.Role = newRole;
                _dbContext.SaveChanges();

                var notification = new Notification
                {
                    Id = userId,
                    NewRole = newRole,
                    Message = $"User ID {userId} role changed to {newRole}"
                };

                if (UserConnections.TryGetValue(userId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("SendMessage", notification);
                }
            }
            else
            {
                throw new Exception($"User with ID {userId} not found");
            }
        }
    }
}
