using System.Collections.Concurrent;
using DemoProject.Contexts;
using DemoProject.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DemoProject.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly AppDbContext _dbContext;

        // Dictionary to store multiple connection IDs per user
        private static readonly ConcurrentDictionary<int, List<string>> UserConnections = new();

        public NotificationHub(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override async Task OnConnectedAsync()
        {
            var userIdString = Context.GetHttpContext()?.Request.Query["userId"];
            var role = Context.GetHttpContext()?.Request.Query["role"];
            var sessionIdString = Context.GetHttpContext()?.Request.Query["sessionId"];

            if (int.TryParse(userIdString, out int userId) && Guid.TryParse(sessionIdString, out Guid sessionId))
            {
                // Update the UserSession with the new ConnectionId
                var session = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                if (session != null)
                {
                    session.ConnectionId = Context.ConnectionId;
                    await _dbContext.SaveChangesAsync();
                }

                //Add connection to the user's connection list
                lock (UserConnections)
                {
                    if (!UserConnections.ContainsKey(userId))
                    {
                        UserConnections[userId] = new List<string>();
                    }
                    UserConnections[userId].Add(Context.ConnectionId);
                }

                // Add user to a group named "User_{userId}"
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                Console.WriteLine($"User added to group: ConnectionId = {Context.ConnectionId}");
            }

            // Check if the user is an admin and add to the "Admins" group
            if (role.HasValue && role.ToString() == "Admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
                Console.WriteLine($"Admin added to group: ConnectionId = {Context.ConnectionId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdString = Context.GetHttpContext()?.Request.Query["userId"];
            var role = Context.GetHttpContext()?.Request.Query["role"];

            if (int.TryParse(userIdString, out int userId))
            {
                // Remove connection from the user's connection list
                lock (UserConnections)
                {
                    if (UserConnections.ContainsKey(userId))
                    {
                        UserConnections[userId].Remove(Context.ConnectionId);
                        if (!UserConnections[userId].Any())
                        {
                            UserConnections.TryRemove(userId, out _);
                        }
                    }
                }

                // Remove user from their personal group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            // Remove admin connection from the "Admins" group
            if (role.HasValue && role.ToString() == "Admin")
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task ChangeUserRole(int userId, string newRole)
        {
            // Fetch the user from the database
            var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                // Handle case where user does not exist
                await Clients.Caller.SendAsync("SendMessage", new { message = "User not found.", result = false });
                return;
            }

            // Update the user's role
            user.Role = newRole;

            // Save changes to the database
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            // Create a notification object
            var notification = new
            {
                Id = user.Id,
                NewRole = user.Role,
                Message = $"User {user.Username} role updated to {newRole}."
            };

            // Broadcast the role change to all admins
            await Clients.All.SendAsync("SendMessage", notification);

            // Notify only the specific affected user
            var connectionId = _dbContext.UserSessions.FirstOrDefault(c => c.UserId == userId)?.ConnectionId;
            if (!string.IsNullOrEmpty(connectionId))
            {
                await Clients.Client(connectionId).SendAsync("SendMessage", notification);
            }
        }

       
    }
}
