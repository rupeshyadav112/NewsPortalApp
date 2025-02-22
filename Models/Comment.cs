namespace NewsPortalApp.Models
{
    public class Comment
    {
        public int CommentID { get; set; }
        public int PostID { get; set; }
        public int UserID { get; set; }
        public string CommentText { get; set; }
        public int NumberOfLikes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ModifiedAt { get; set; }
        // Navigation Properties

        public Post Post { get; set; } // Each comment belongs to one post
        public User User { get; set; } // Each comment is made by one user
        public ICollection<CommentLike> Likes { get; set; } // A comment can have many likes
    }
}
