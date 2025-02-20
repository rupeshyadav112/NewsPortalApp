namespace NewsPortalApp.Models
{
    public class RecentPost
    {
        public int PostID { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string PostImage { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
