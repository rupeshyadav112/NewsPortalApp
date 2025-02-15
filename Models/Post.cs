namespace NewsPortalApp.Models
{
    public class Post
    {
        public int PostID { get; set; }
        public string? Title { get; set; } // Add '?' to make it nullable
        public string? Content { get; set; }
        public string? Category { get; set; }
        public string? ImagePath { get; set; }
        public string? FontStyle { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
