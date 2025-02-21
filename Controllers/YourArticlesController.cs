using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.Models;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;

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

            // Pass categories to the view
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

        // POST: YourArticles/Edit/{id}
        [HttpPost]
        public IActionResult Edit(Post post, IFormFile fileUpload)
        {
            if (ModelState.IsValid)
            {
                // Handle file upload
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
                    // If no new file is uploaded, retain the existing image path
                    var existingPost = LoadPosts().FirstOrDefault(p => p.PostID == post.PostID);
                    if (existingPost != null)
                    {
                        post.ImagePath = existingPost.ImagePath;
                    }
                }

                // Update the post in the database
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

            // If the model state is invalid, reload the categories and return the view
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
    }
}