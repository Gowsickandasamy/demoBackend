﻿namespace DemoProject.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }

        public ICollection<UserSession> UserSessions { get; set; }
    }

}
