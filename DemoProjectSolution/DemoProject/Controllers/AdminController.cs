using DemoProject.Contexts;
using DemoProject.DTOs;
using DemoProject.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DemoProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly IHubContext<TerminationNotificationHub> _hubContext;
        private readonly ILogger<AdminController> _logger;



        public AdminController(AppDbContext context, IHubContext<TerminationNotificationHub> hubContext, ILogger<AdminController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }
        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            var users = _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Role
                })
                .ToList();

            return Ok(new { Message = "Users retrieved Succesfully", users });
        }

        [HttpGet("OnlineUsers")]
        public async Task<IActionResult> GetOnlineUsers()
        {
            _logger.LogInformation("Fetching online users");

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

            _logger.LogInformation($"Found {onlineUsers.Count} online users");

            foreach (var user in onlineUsers)
            {
                _logger.LogInformation($"User: {user.Username}, ConnectionId: {user.ConnectionId ?? "null"}");
            }

            return Ok(onlineUsers);
        }

        [HttpPost("NotifySession")]
        public async Task<IActionResult> NotifySession([FromBody] TerminateSessionRequest notifySessionDto)
        {
            _logger.LogInformation($"Received notification request for SessionId: {notifySessionDto.SessionId}");

            // Fetch the session from the database
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(us => us.SessionId == notifySessionDto.SessionId && us.IsActive);

            if (session == null)
            {
                _logger.LogWarning($"Session not found or inactive: {notifySessionDto.SessionId}");
                return NotFound(new { message = "Session not found or inactive" });
            }

            if (string.IsNullOrEmpty(session.ConnectionId))
            {
                _logger.LogWarning($"ConnectionId not available for SessionId: {notifySessionDto.SessionId}");
                return BadRequest(new { message = "Connection ID not available for the session" });
            }

            var message = $"Session {session.SessionId} is notified.";
            _logger.LogInformation($"Notifying ConnectionId: {session.ConnectionId} with message: {message}");


            try
            {
                // Send the notification to the specific client
                await _hubContext.Clients.Client(session.ConnectionId).SendAsync("ReceiveNotification", message);
                session.IsActive = false;
                session.LogoutTime = DateTime.UtcNow; // Optionally record logout time
                _context.UserSessions.Update(session);
                _context.SaveChanges();

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

                // Broadcast the updated list of online users to all connected clients
                await _hubContext.Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers);

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying ConnectionId: {session.ConnectionId}");
                return StatusCode(500, new { message = "Failed to send notification." });
            }
        }
    }
}
