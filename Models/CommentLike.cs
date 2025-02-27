using System.ComponentModel.DataAnnotations;

namespace NewsPortalApp.Models
{
    public class CommentLike
    {
        [Key]
        public int LikeID { get; set; }
        public int CommentID { get; set; } // Foreign key to Comment
        public int UserID { get; set; } // Foreign key to User
        public DateTime CreatedAt { get; set; } = DateTime.Now;
     
        public Comment Comment { get; set; } // Each like belongs to one comment
        public User User { get; set; } // Each like is made by one user
    }
}
