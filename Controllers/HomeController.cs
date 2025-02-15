using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NewsPortalApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace NewsPortalApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            // Check if user is logged in
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (userEmail != null)
            {
                ViewBag.UserEmail = userEmail;
                ViewBag.ProfileImagePath = HttpContext.Session.GetString("UserProfileImage") ?? "~/image/Avatar.png";
            }

            // Load recent posts
            var recentPosts = LoadRecentPosts();
            return View(recentPosts);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Logout action
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Helper method to load recent posts from the database
        private List<Post> LoadRecentPosts()
        {
            var posts = new List<Post>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"SELECT TOP 5 PostID, Title, Content, Category, ImagePath, FontStyle, CreatedAt 
                                      FROM Posts 
                                      WHERE PostID IS NOT NULL
                                      ORDER BY CreatedAt DESC";

                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                posts.Add(new Post
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
                        _logger.LogError(ex, "Error loading recent posts from the database.");
                    }
                }
            }

            return posts;
        }
    }
}