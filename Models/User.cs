using System.ComponentModel.DataAnnotations;

namespace NewsPortalApp.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string ProfileImagePath { get; set; }
        public string FullName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string GoogleUserId { get; set; }
        public bool IsGoogleAccount { get; set; }
        // Navigation Properties

        public ICollection<Comment> Comments { get; set; } // A user can make many comments
        public ICollection<CommentLike> Likes { get; set; } // A user can like many comments

    }
}
