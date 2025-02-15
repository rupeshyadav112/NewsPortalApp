using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NewsPortalApp.Models;
using Microsoft.AspNetCore.Http;

namespace NewsPortalApp.Controllers
{
    public class NewsArticlesController : Controller
    {
        private readonly IConfiguration _configuration;

        public NewsArticlesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            // Check if user is logged in
            if (HttpContext.Session.GetString("UserEmail") != null)
            {
                ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
                ViewBag.ProfileImagePath = HttpContext.Session.GetString("UserProfileImage") ?? "~/image/Avatar.png";
            }

            // Load all posts
            var articles = LoadAllPosts();
            return View("NewsArticles", articles);
        }


        [HttpPost]
        public IActionResult ApplyFilters(string searchTerm, string sortBy, string category)
        {
            var articles = LoadFilteredPosts(searchTerm, sortBy, category);
            return View("Index", articles);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Clear session data
            return RedirectToAction("Index", "Home"); // Redirect to home page
        }

        // Helper method to load all posts
        private List<Article> LoadAllPosts()
        {
            return LoadFilteredPosts(null, null, null);
        }

        // Helper method to load filtered posts
        private List<Article> LoadFilteredPosts(string searchTerm, string sortBy, string category)
        {
            var articles = new List<Article>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT PostID, Title, Content, Category, ImagePath, FontStyle, CreatedAt FROM Posts WHERE 1=1";

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                    query += " AND (Title LIKE @SearchTerm OR Content LIKE @SearchTerm)";

                if (!string.IsNullOrEmpty(category))
                    query += " AND Category = @Category";

                // Apply sorting
                switch (sortBy)
                {
                    case "date":
                        query += " ORDER BY CreatedAt DESC";
                        break;
                    case "popularity":
                        query += " ORDER BY CreatedAt DESC"; // Replace with actual popularity logic if available
                        break;
                    default:
                        query += " ORDER BY CreatedAt DESC";
                        break;
                }

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(searchTerm))
                        cmd.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");

                    if (!string.IsNullOrEmpty(category))
                        cmd.Parameters.AddWithValue("@Category", category);

                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                articles.Add(new Article
                                {
                                    PostID = reader.GetInt32(0),
                                    Title = reader.GetString(1),
                                    Content = reader.GetString(2),
                                    Category = reader.GetString(3),
                                    ImagePath = reader.GetString(4),
                                    FontStyle = reader.GetString(5),
                                    CreatedAt = reader.GetDateTime(6)
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }

            return articles;
        }
    }
}