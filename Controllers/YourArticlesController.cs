using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NewsPortalApp.Models;
using System.Security.Claims;
using System.Text.Json;


namespace NewsPortalApp.Controllers
{
    public class YourArticlesController : Controller
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpClientFactory _httpClientFactory;

        public YourArticlesController(IConfiguration configuration, IWebHostEnvironment webHostEnvironment, IHttpClientFactory httpClientFactory)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webHostEnvironment = webHostEnvironment;
            _httpClientFactory = httpClientFactory;
        }

        // GET: /YourArticles - सभी पोस्ट्स की लिस्ट दिखाना
        [Route("YourArticles")]
        public IActionResult Index()
        {
            var posts = LoadPosts();
            return View(posts);
        }

        // GET: /YourArticles/Index/{id} - एक पोस्ट और लेटेस्ट न्यूज़ दिखाना
        [Route("YourArticles/Index/{id}")]
        public async Task<IActionResult> Index(int id)
        {
            var post = LoadPost(id);
            if (post == null)
            {
                Console.WriteLine($"Post with ID {id} not found");
                return NotFound();
            }

            var newsArticles = await FetchLatestNews();
            ViewBag.NewsArticles = newsArticles;
            Console.WriteLine($"NewsArticles count in Index: {newsArticles.Count}");

            ViewBag.RecentArticles = LoadRecentArticles(id);
            return View("Post", post); // "Post" व्यू यूज़ करें
        }

        private async Task<List<NewsArticle>> FetchLatestNews()
        {
            var client = _httpClientFactory.CreateClient();
            var apiKey = "ff175d1b56cb4ca1839b6f76391f1b95";
            var url = $"https://newsapi.org/v2/top-headlines?country=us&q=news&apiKey={apiKey}";

            try
            {
                var response = await client.GetAsync(url);
                Console.WriteLine($"NewsAPI response status: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch news: {response.StatusCode} - {response.ReasonPhrase}");
                    return new List<NewsArticle>();
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"NewsAPI response: {jsonString.Substring(0, Math.Min(jsonString.Length, 500))}...");
                var jsonDoc = JsonDocument.Parse(jsonString);
                var articles = jsonDoc.RootElement.GetProperty("articles").EnumerateArray();

                var newsList = new List<NewsArticle>();
                foreach (var article in articles)
                {
                    newsList.Add(new NewsArticle
                    {
                        Title = article.GetProperty("title").GetString(),
                        Description = article.GetProperty("description").GetString() ?? "No description available",
                        Url = article.GetProperty("url").GetString(),
                        PublishedAt = article.GetProperty("publishedAt").GetDateTime()
                    });
                }
                Console.WriteLine($"Fetched {newsList.Count} news articles");
                return newsList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching news: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                return new List<NewsArticle>();
            }
        }

        // बाकी कोड वही रहेगा (संक्षिप्तता के लिए हटाया गया)
        private List<Post> LoadPosts()
        {
            var posts = new List<Post>();
            using (var conn = new SqlConnection(_connectionString))
            {
                var cmd = new SqlCommand("SELECT PostID, Title, Content, Category, ImagePath, CreatedAt FROM Posts ORDER BY CreatedAt DESC", conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    posts.Add(new Post
                    {
                        PostID = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        Content = reader.GetString(2),
                        Category = reader.GetString(3),
                        ImagePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5)
                    });
                }
            }
            return posts;
        }

        private Post LoadPost(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var post = new Post();
                var cmd = new SqlCommand("SELECT PostID, Title, Content, Category, ImagePath, CreatedAt FROM Posts WHERE PostID = @PostID", conn);
                cmd.Parameters.AddWithValue("@PostID", id);
                conn.Open();
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    post.PostID = reader.GetInt32(0);
                    post.Title = reader.GetString(1);
                    post.Content = reader.GetString(2);
                    post.Category = reader.GetString(3);
                    post.ImagePath = reader.IsDBNull(4) ? null : reader.GetString(4);
                    post.CreatedAt = reader.GetDateTime(5);
                }
                else
                {
                    return null;
                }
                reader.Close();

                post.Comments = new List<Comment>();
                cmd = new SqlCommand("SELECT CommentID, PostID, UserID, CommentText, NumberOfLikes, CreatedAt, ModifiedAt, ParentCommentID FROM Comments WHERE PostID = @PostID ORDER BY CreatedAt DESC", conn);
                cmd.Parameters.AddWithValue("@PostID", id);
                var commentReader = cmd.ExecuteReader();
                while (commentReader.Read())
                {
                    var comment = new Comment
                    {
                        CommentID = commentReader.GetInt32(0),
                        PostID = commentReader.GetInt32(1),
                        UserID = commentReader.GetInt32(2),
                        CommentText = commentReader.GetString(3),
                        NumberOfLikes = commentReader.GetInt32(4),
                        CreatedAt = commentReader.GetDateTime(5),
                        ModifiedAt = commentReader.IsDBNull(6) ? (DateTime?)null : commentReader.GetDateTime(6),
                        ParentCommentID = commentReader.IsDBNull(7) ? (int?)null : commentReader.GetInt32(7),
                        User = LoadUser(commentReader.GetInt32(2)),
                        Likes = LoadLikes(commentReader.GetInt32(0))
                    };
                    post.Comments.Add(comment);
                }
                return post;
            }
        }

        private User LoadUser(int userId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var cmd = new SqlCommand("SELECT UserID, Username, Email, ProfileImagePath FROM Users WHERE UserID = @UserID", conn);
                cmd.Parameters.AddWithValue("@UserID", userId);
                conn.Open();
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new User
                    {
                        UserID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        Email = reader.GetString(2),
                        ProfileImagePath = reader.IsDBNull(3) ? null : reader.GetString(3)
                    };
                }
                return null;
            }
        }

        private List<CommentLike> LoadLikes(int commentId)
        {
            var likes = new List<CommentLike>();
            using (var conn = new SqlConnection(_connectionString))
            {
                var cmd = new SqlCommand("SELECT LikeID, CommentID, UserID, CreatedAt FROM CommentLikes WHERE CommentID = @CommentID", conn);
                cmd.Parameters.AddWithValue("@CommentID", commentId);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    likes.Add(new CommentLike
                    {
                        LikeID = reader.GetInt32(0),
                        CommentID = reader.GetInt32(1),
                        UserID = reader.GetInt32(2),
                        CreatedAt = reader.GetDateTime(3)
                    });
                }
            }
            return likes;
        }

        private List<Post> LoadRecentArticles(int excludeId)
        {
            var posts = new List<Post>();
            using (var conn = new SqlConnection(_connectionString))
            {
                var cmd = new SqlCommand("SELECT TOP 3 PostID, Title, Category, ImagePath, CreatedAt FROM Posts WHERE PostID != @ExcludeID ORDER BY CreatedAt DESC", conn);
                cmd.Parameters.AddWithValue("@ExcludeID", excludeId);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    posts.Add(new Post
                    {
                        PostID = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        Category = reader.GetString(2),
                        ImagePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                        CreatedAt = reader.GetDateTime(4)
                    });
                }
            }
            return posts;
        }

        [HttpPost]
        public IActionResult Delete(int postId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var cmd = new SqlCommand("DELETE FROM Posts WHERE PostID = @PostID", conn);
                cmd.Parameters.AddWithValue("@PostID", postId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("Index");
        }

        public IActionResult Edit(int id)
        {
            var post = LoadPosts().FirstOrDefault(p => p.PostID == id);
            if (post == null)
            {
                return NotFound();
            }

            ViewBag.Categories = new List<string>
            {
                "Technology",
                "Health",
                "Sports",
                "Entertainment",
                "World News",
                "Local News"
            };

            return View(post);
        }

        [HttpPost]
        public IActionResult Edit(Post post, IFormFile fileUpload)
        {
            if (ModelState.IsValid)
            {
                if (fileUpload != null && fileUpload.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + fileUpload.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        fileUpload.CopyTo(fileStream);
                    }

                    post.ImagePath = "/uploads/" + uniqueFileName;
                }
                else
                {
                    var existingPost = LoadPosts().FirstOrDefault(p => p.PostID == post.PostID);
                    if (existingPost != null)
                    {
                        post.ImagePath = existingPost.ImagePath;
                    }
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    var cmd = new SqlCommand("UPDATE Posts SET Title = @Title, Content = @Content, Category = @Category, ImagePath = @ImagePath WHERE PostID = @PostID", conn);
                    cmd.Parameters.AddWithValue("@Title", post.Title);
                    cmd.Parameters.AddWithValue("@Content", post.Content);
                    cmd.Parameters.AddWithValue("@Category", post.Category);
                    cmd.Parameters.AddWithValue("@ImagePath", post.ImagePath ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PostID", post.PostID);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                return RedirectToAction("Index");
            }

            ViewBag.Categories = new List<string>
            {
                "Technology",
                "Health",
                "Sports",
                "Entertainment",
                "World News",
                "Local News"
            };
            return View(post);
        }

        public IActionResult AllUsers()
        {
            var users = LoadUsers();
            return View(users);
        }

        private List<User> LoadUsers()
        {
            var users = new List<User>();

            using (var conn = new SqlConnection(_connectionString))
            {
                var cmd = new SqlCommand("SELECT UserID, Username, Email, ProfileImagePath, CreatedAt FROM Users ORDER BY CreatedAt DESC", conn);
                conn.Open();
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    users.Add(new User
                    {
                        UserID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        Email = reader.GetString(2),
                        ProfileImagePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                        CreatedAt = reader.GetDateTime(4)
                    });
                }
            }

            return users;
        }

        public IActionResult AllComment()
        {
            var comments = LoadComments();
            return View(comments);
        }

        private List<Comment> LoadComments()
        {
            var comments = new List<Comment>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    var cmd = new SqlCommand("SELECT CommentID, PostID, UserID, CommentText, NumberOfLikes, CreatedAt, ModifiedAt, ParentCommentID FROM Comments ORDER BY CreatedAt DESC", conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        comments.Add(new Comment
                        {
                            CommentID = reader.GetInt32(0),
                            PostID = reader.GetInt32(1),
                            UserID = reader.GetInt32(2),
                            CommentText = reader.GetString(3),
                            NumberOfLikes = reader.GetInt32(4),
                            CreatedAt = reader.GetDateTime(5),
                            ModifiedAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                            ParentCommentID = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading comments: " + ex.Message);
            }

            return comments;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddComment(int postId, [FromBody] CommentDto commentDto, int? parentCommentId = null)
        {
            Console.WriteLine($"AddComment called: postId={postId}, commentText={commentDto?.CommentText}, parentCommentId={parentCommentId}");

            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"User ID from claims: {userId}");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
            {
                Console.WriteLine("Invalid user ID");
                return BadRequest("Invalid user ID.");
            }

            if (string.IsNullOrEmpty(commentDto?.CommentText))
            {
                Console.WriteLine("Comment text is empty");
                return BadRequest("Comment text cannot be empty.");
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    var cmd = new SqlCommand("INSERT INTO Comments (PostID, UserID, CommentText, CreatedAt, NumberOfLikes, ParentCommentID) VALUES (@PostID, @UserID, @CommentText, @CreatedAt, @NumberOfLikes, @ParentCommentID)", conn);
                    cmd.Parameters.AddWithValue("@PostID", postId);
                    cmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    cmd.Parameters.AddWithValue("@CommentText", commentDto.CommentText);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("@NumberOfLikes", 0);
                    cmd.Parameters.AddWithValue("@ParentCommentID", parentCommentId.HasValue ? (object)parentCommentId.Value : DBNull.Value);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("Comment added successfully");
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding comment: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditComment(int commentId, [FromBody] CommentDto commentDto)
        {
            Console.WriteLine($"EditComment called: commentId={commentId}, commentText={commentDto?.CommentText}");

            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"User ID from claims: {userId}");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
            {
                Console.WriteLine("Invalid user ID");
                return BadRequest("Invalid user ID.");
            }

            if (string.IsNullOrEmpty(commentDto?.CommentText))
            {
                Console.WriteLine("Comment text is empty");
                return BadRequest("Comment text cannot be empty.");
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Comments WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    checkCmd.Parameters.AddWithValue("@CommentID", commentId);
                    checkCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    conn.Open();
                    int count = (int)checkCmd.ExecuteScalar();
                    conn.Close();

                    if (count == 0)
                    {
                        Console.WriteLine($"Comment {commentId} not found or not owned by user {parsedUserId}");
                        return NotFound("Comment not found.");
                    }

                    var cmd = new SqlCommand("UPDATE Comments SET CommentText = @CommentText, ModifiedAt = @ModifiedAt WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    cmd.Parameters.AddWithValue("@CommentText", commentDto.CommentText);
                    cmd.Parameters.AddWithValue("@ModifiedAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("@CommentID", commentId);
                    cmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("Comment updated successfully");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating comment: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteComment(int commentId)
        {
            Console.WriteLine($"DeleteComment called: commentId={commentId}");

            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"User ID from claims: {userId}");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
            {
                Console.WriteLine("Invalid user ID");
                return BadRequest("Invalid user ID.");
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var checkExistsCmd = new SqlCommand("SELECT COUNT(*) FROM Comments WHERE CommentID = @CommentID", conn);
                    checkExistsCmd.Parameters.AddWithValue("@CommentID", commentId);
                    int existsCount = (int)checkExistsCmd.ExecuteScalar();
                    if (existsCount == 0)
                    {
                        Console.WriteLine($"Comment {commentId} does not exist in the database");
                        return NotFound($"Comment {commentId} does not exist.");
                    }

                    var checkOwnerCmd = new SqlCommand("SELECT COUNT(*) FROM Comments WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    checkOwnerCmd.Parameters.AddWithValue("@CommentID", commentId);
                    checkOwnerCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    int ownerCount = (int)checkOwnerCmd.ExecuteScalar();
                    if (ownerCount == 0)
                    {
                        Console.WriteLine($"Comment {commentId} not owned by user {parsedUserId}");
                        return NotFound($"Comment {commentId} not owned by user {parsedUserId}.");
                    }

                    var deleteLikesCmd = new SqlCommand("DELETE FROM CommentLikes WHERE CommentID = @CommentID", conn);
                    deleteLikesCmd.Parameters.AddWithValue("@CommentID", commentId);
                    deleteLikesCmd.ExecuteNonQuery();
                    Console.WriteLine($"Deleted all likes for CommentID {commentId}");

                    var cmd = new SqlCommand("DELETE FROM Comments WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    cmd.Parameters.AddWithValue("@CommentID", commentId);
                    cmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("Comment deleted successfully");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting comment: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleLike(int commentId)
        {
            Console.WriteLine($"ToggleLike called: commentId={commentId}");

            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"User ID from claims: {userId}");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
            {
                Console.WriteLine("Invalid user ID");
                return BadRequest("Invalid user ID.");
            }

            int currentLikes;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var checkCmd = new SqlCommand("SELECT NumberOfLikes FROM Comments WHERE CommentID = @CommentID", conn);
                    checkCmd.Parameters.AddWithValue("@CommentID", commentId);
                    var result = checkCmd.ExecuteScalar();
                    if (result == null)
                    {
                        Console.WriteLine($"Comment {commentId} not found");
                        return NotFound("Comment not found.");
                    }
                    currentLikes = Convert.ToInt32(result);

                    var likeCheckCmd = new SqlCommand("SELECT COUNT(*) FROM CommentLikes WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    likeCheckCmd.Parameters.AddWithValue("@CommentID", commentId);
                    likeCheckCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    int likeCount = (int)likeCheckCmd.ExecuteScalar();

                    if (likeCount == 0)
                    {
                        var insertCmd = new SqlCommand("INSERT INTO CommentLikes (CommentID, UserID, CreatedAt) VALUES (@CommentID, @UserID, @CreatedAt)", conn);
                        insertCmd.Parameters.AddWithValue("@CommentID", commentId);
                        insertCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        insertCmd.ExecuteNonQuery();
                        currentLikes++;
                    }
                    else
                    {
                        var deleteCmd = new SqlCommand("DELETE FROM CommentLikes WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                        deleteCmd.Parameters.AddWithValue("@CommentID", commentId);
                        deleteCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                        deleteCmd.ExecuteNonQuery();
                        currentLikes--;
                    }

                    var updateCmd = new SqlCommand("UPDATE Comments SET NumberOfLikes = @NumberOfLikes WHERE CommentID = @CommentID", conn);
                    updateCmd.Parameters.AddWithValue("@NumberOfLikes", currentLikes);
                    updateCmd.Parameters.AddWithValue("@CommentID", commentId);
                    updateCmd.ExecuteNonQuery();
                }
                Console.WriteLine("Like toggled successfully");
                return Json(new { success = true, numberOfLikes = currentLikes });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling like: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        public class CommentDto
        {
            public string CommentText { get; set; }
        }
    }

    // NewsArticle क्लास यहाँ डिफाइन की गई
    public class NewsArticle
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public DateTime PublishedAt { get; set; }
    }
}