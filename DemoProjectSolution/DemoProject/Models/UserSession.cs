using System.Text.Json.Serialization;

namespace DemoProject.Models
{
    public class UserSession
    {
        public Guid SessionId { get; set; } = Guid.NewGuid(); // Primary Key
        public int UserId { get; set; } // Foreign Key
        public string DeviceInfo { get; set; }
        public string ConnectionId { get; set; }
        public string IPAddress { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; } // Nullable for users who haven't logged out yet

        public bool IsActive { get; set; }

        // Navigation Property
        [JsonIgnore]
        public User User { get; set; }
    }

}
