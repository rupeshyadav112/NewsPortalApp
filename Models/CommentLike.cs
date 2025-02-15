using System.ComponentModel.DataAnnotations;

namespace NewsPortalApp.Models
{
    public class CommentLike
    {
        [Key]
        public int LikeID { get; set; }
        public int? CommentID { get; set; }
        public int? UserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // Navigation Properties
        public Comment Comment { get; set; }
        public User User { get; set; }
    }
}
