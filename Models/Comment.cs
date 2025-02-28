using System;
using System.Collections.Generic;

namespace NewsPortalApp.Models
{
    public class Comment
    {
        public int CommentID { get; set; }
        public int PostID { get; set; }
        public int UserID { get; set; }
        public string CommentText { get; set; }
        public int NumberOfLikes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ParentCommentID { get; set; } // नया प्रॉपर्टी जोड़ा गया

        // Navigation properties (optional, if you want to include them)
        public Post Post { get; set; }
        public User User { get; set; }
        public ICollection<CommentLike> Likes { get; set; }
        public Comment ParentComment { get; set; } // पैरेंट कमेंट के लिए (ऑप्शनल)
        public ICollection<Comment> Replies { get; set; } // रिप्लाईज़ के लिए (ऑप्शनल)
    }
}