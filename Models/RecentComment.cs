namespace NewsPortalApp.Models
{
    public class RecentComment
    {

        public int CommentID { get; set; }
        public string CommentText { get; set; }
        public string Username { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
