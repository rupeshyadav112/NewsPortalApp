﻿namespace NewsPortalApp.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string? ProfileImagePath { get; set; }
        public string? FullName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? GoogleUserId { get; set; }
        public bool IsGoogleAccount { get; set; } = false;
    }
}
