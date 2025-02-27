using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NewsPortalApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace NewsPortalApp.Controllers
{
    public class YourArticlesController : Controller
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public YourArticlesController(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: YourArticles
        public IActionResult Index()
        {
            var posts = LoadPosts();
            return View(posts);
        }

        // Load posts from the database
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

        // POST: YourArticles/Delete/{postId}
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

        // GET: YourArticles/Edit/{id}
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

        // POST: YourArticles/Edit/{id}
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

        // GET: YourArticles/AllUsers
        public IActionResult AllUsers()
        {
            var users = LoadUsers();
            return View(users);
        }

        // Load users from the database
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

        // GET: YourArticles/AllComment
        public IActionResult AllComment()
        {
            var comments = LoadComments();
            return View(comments);
        }

        // Load comments from the database
        private List<Comment> LoadComments()
        {
            var comments = new List<Comment>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    var cmd = new SqlCommand("SELECT CommentID, PostID, UserID, CommentText, NumberOfLikes, CreatedAt, ModifiedAt FROM Comments ORDER BY CreatedAt DESC", conn);
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
                            ModifiedAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
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

        // POST: YourArticles/AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddComment(int postId, [FromBody] CommentDto commentDto)
        {
            Console.WriteLine($"AddComment called: postId={postId}, commentText={commentDto?.CommentText}");

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
                    var cmd = new SqlCommand("INSERT INTO Comments (PostID, UserID, CommentText, CreatedAt, NumberOfLikes) VALUES (@PostID, @UserID, @CommentText, @CreatedAt, @NumberOfLikes)", conn);
                    cmd.Parameters.AddWithValue("@PostID", postId);
                    cmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    cmd.Parameters.AddWithValue("@CommentText", commentDto.CommentText);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("@NumberOfLikes", 0);
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

        // POST: YourArticles/EditComment
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
                    // पहले चेक करें कि कमेंट मौजूद है और यूज़र का है
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

                    // कमेंट अपडेट करें
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

        // POST: YourArticles/DeleteComment/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteComment(int id)
        {
            Console.WriteLine($"DeleteComment called: commentId={id}");

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
                    // पहले चेक करें कि कमेंट मौजूद है और यूज़र का है
                    var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Comments WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    checkCmd.Parameters.AddWithValue("@CommentID", id);
                    checkCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    conn.Open();
                    int count = (int)checkCmd.ExecuteScalar();
                    conn.Close();

                    if (count == 0)
                    {
                        Console.WriteLine($"Comment {id} not found or not owned by user {parsedUserId}");
                        return NotFound("Comment not found.");
                    }

                    // कमेंट डिलीट करें
                    var cmd = new SqlCommand("DELETE FROM Comments WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    cmd.Parameters.AddWithValue("@CommentID", id);
                    cmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    conn.Open();
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

        // POST: YourArticles/ToggleLike
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

            int currentLikes; // currentLikes को यहाँ डिफाइन किया ताकि स्कोप सही हो

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // चेक करें कि कमेंट मौजूद है और NumberOfLikes लोड करें
                    var checkCmd = new SqlCommand("SELECT NumberOfLikes FROM Comments WHERE CommentID = @CommentID", conn);
                    checkCmd.Parameters.AddWithValue("@CommentID", commentId);
                    var result = checkCmd.ExecuteScalar();
                    if (result == null)
                    {
                        Console.WriteLine($"Comment {commentId} not found");
                        return NotFound("Comment not found.");
                    }
                    currentLikes = Convert.ToInt32(result);

                    // चेक करें कि यूज़र ने पहले लाइक किया है
                    var likeCheckCmd = new SqlCommand("SELECT COUNT(*) FROM CommentLikes WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                    likeCheckCmd.Parameters.AddWithValue("@CommentID", commentId);
                    likeCheckCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                    int likeCount = (int)likeCheckCmd.ExecuteScalar();

                    if (likeCount == 0)
                    {
                        // लाइक जोड़ें
                        var insertCmd = new SqlCommand("INSERT INTO CommentLikes (CommentID, UserID, CreatedAt) VALUES (@CommentID, @UserID, @CreatedAt)", conn);
                        insertCmd.Parameters.AddWithValue("@CommentID", commentId);
                        insertCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        insertCmd.ExecuteNonQuery();

                        // NumberOfLikes बढ़ाएँ
                        currentLikes++;
                    }
                    else
                    {
                        // लाइक हटाएँ
                        var deleteCmd = new SqlCommand("DELETE FROM CommentLikes WHERE CommentID = @CommentID AND UserID = @UserID", conn);
                        deleteCmd.Parameters.AddWithValue("@CommentID", commentId);
                        deleteCmd.Parameters.AddWithValue("@UserID", parsedUserId);
                        deleteCmd.ExecuteNonQuery();

                        // NumberOfLikes घटाएँ
                        currentLikes--;
                    }

                    // NumberOfLikes अपडेट करें
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

        // DTO for comment submission
        public class CommentDto
        {
            public string CommentText { get; set; }
        }
    }
}