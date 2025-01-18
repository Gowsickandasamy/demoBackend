using DemoProject.Contexts;
using DemoProject.DTOs;
using DemoProject.Hubs;
using DemoProject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DemoProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<TerminationNotificationHub> _hubContext;

        public AuthController(AppDbContext context, IHubContext<TerminationNotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto loginDto)
        {
            // Check if the user exists in the database
            var user = _context.Users.SingleOrDefault(u =>
                u.Username == loginDto.Username && u.Password == loginDto.Password);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()
                            ?? HttpContext.Connection.RemoteIpAddress?.ToString();
            var connectionId = Guid.NewGuid().ToString();
            var deviceInfo = loginDto.DeviceInfo ?? "Unknown Device";

            var userSession = new UserSession
            {
                UserId = user.Id, // Foreign key from Users table
                DeviceInfo = deviceInfo,
                IPAddress = ipAddress,
                LoginTime = DateTime.UtcNow,
                IsActive = true,
                ConnectionId = connectionId
            };

            _context.UserSessions.Add(userSession);
            _context.SaveChanges();

            // Return user details
            return Ok(new { message = "Login successful",
                user = new { user.Id, user.Username, user.Role, userSession.SessionId },
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutDto logoutDto)
        {
            // Find the active session for the given user
            var session = _context.UserSessions
                    .FirstOrDefault(s => s.SessionId == logoutDto.SessionId && s.IsActive);


            if (session == null)
            {
                return NotFound(new { message = "Active session not found for the user" });
            }

            // Update the session status
            session.IsActive = false;
            session.LogoutTime = DateTime.UtcNow; // Optionally record logout time
            _context.UserSessions.Update(session);
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

            // Notify all clients about the updated online users
            await _hubContext.Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers);
            _context.SaveChanges();

            return Ok(new { message = "Logout successful" });
        }
    }

}

