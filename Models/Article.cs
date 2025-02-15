namespace NewsPortalApp.Models
{
    public class Article
    {
        public int PostID { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Category { get; set; }
        public string ImagePath { get; set; }
        public string FontStyle { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
