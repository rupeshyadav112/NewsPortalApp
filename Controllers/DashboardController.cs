using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.Models;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace NewsPortalApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly string _connectionString;

        public DashboardController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UserEmail") == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            var dashboardStats = LoadDashboardStats();
            var recentUsers = LoadRecentUsers();
            var recentPosts = LoadRecentPosts();
            var recentComments = LoadRecentComments();

            ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
            ViewBag.UserProfileImage = HttpContext.Session.GetString("UserProfileImage");
            ViewBag.DashboardStats = dashboardStats;
            ViewBag.RecentUsers = recentUsers;
            ViewBag.RecentPosts = recentPosts;
            ViewBag.RecentComments = recentComments;

            return View();
        }


        // नया मेथड: Recent Comments लोड करने के लिए
        private List<RecentComment> LoadRecentComments()
        {
            var comments = new List<RecentComment>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT TOP 5 
                        c.CommentID,
                        c.CommentText,
                        u.Username,
                        c.CreatedAt
                    FROM Comments c
                    INNER JOIN Users u ON c.UserID = u.UserID
                    ORDER BY c.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            comments.Add(new RecentComment
                            {
                                CommentID = Convert.ToInt32(reader["CommentID"]),
                                CommentText = reader["CommentText"].ToString(),
                                Username = reader["Username"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            });
                        }
                    }
                }
            }
            return comments;
        }

        // नया एक्शन: सभी कमेंट्स दिखाने के लिए
        public IActionResult AllComments()
        {
            var allComments = GetAllComments();
            return View(allComments);
        }

        private List<RecentComment> GetAllComments()
        {
            var comments = new List<RecentComment>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT 
                        c.CommentID,
                        c.CommentText,
                        u.Username,
                        c.CreatedAt
                    FROM Comments c
                    INNER JOIN Users u ON c.UserID = u.UserID
                    ORDER BY c.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            comments.Add(new RecentComment
                            {
                                CommentID = Convert.ToInt32(reader["CommentID"]),
                                CommentText = reader["CommentText"].ToString(),
                                Username = reader["Username"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            });
                        }
                    }
                }
            }
            return comments;
        }

        // सर्कुलर प्रोग्रेस बार के लिए अपडेटेड लॉजिक
        public double GetCircleOffset(int value, int maxValue)
        {
            double percentage = maxValue == 0 ? 0 : (double)value / maxValue;
            double circumference = 2 * Math.PI * 70; // 2πr, where r = 70
            return circumference * (1 - percentage);
        }

        private DashboardStats LoadDashboardStats()
        {
            var stats = new DashboardStats();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"
             SELECT 
                 (SELECT COUNT(*) FROM Users) as TotalUsers,
                 (SELECT COUNT(*) FROM Comments) as TotalComments,
                 (SELECT COUNT(*) FROM Posts) as TotalPosts,
                 (SELECT COUNT(*) FROM Users WHERE DATEADD(day, -30, GETDATE()) <= CreatedAt) as LastMonthUsers,
                 (SELECT COUNT(*) FROM Comments WHERE DATEADD(day, -30, GETDATE()) <= CreatedAt) as LastMonthComments,
                 (SELECT COUNT(*) FROM Posts WHERE DATEADD(day, -30, GETDATE()) <= CreatedAt) as LastMonthPosts";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.TotalUsers = Convert.ToInt32(reader["TotalUsers"]);
                            stats.TotalComments = Convert.ToInt32(reader["TotalComments"]);
                            stats.TotalPosts = Convert.ToInt32(reader["TotalPosts"]);
                            stats.LastMonthUsers = Convert.ToInt32(reader["LastMonthUsers"]);
                            stats.LastMonthComments = Convert.ToInt32(reader["LastMonthComments"]);
                            stats.LastMonthPosts = Convert.ToInt32(reader["LastMonthPosts"]);
                        }
                    }
                }
            }

            return stats;
        }

        private List<RecentUser> LoadRecentUsers()
        {
            var users = new List<RecentUser>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"
             SELECT TOP 5 
                 UserID, 
                 Username, 
                 Email, 
                 ISNULL(ProfileImagePath, '~/Images/default-profile.png') as UserImage,
                 CreatedAt
             FROM Users 
             ORDER BY CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new RecentUser
                            {
                                UserID = Convert.ToInt32(reader["UserID"]),
                                Username = reader["Username"].ToString(),
                                Email = reader["Email"].ToString(),
                                UserImage = reader["UserImage"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            });
                        }
                    }
                }
            }

            return users;
        }

        private List<RecentPost> LoadRecentPosts()
        {
            var posts = new List<RecentPost>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"
             SELECT TOP 5 
                 PostID, 
                 Title, 
                 Category, 
                 ISNULL(ImagePath, '~/Images/default-post.png') as PostImage,
                 CreatedAt
             FROM Posts 
             ORDER BY CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            posts.Add(new RecentPost
                            {
                                PostID = Convert.ToInt32(reader["PostID"]),
                                Title = reader["Title"].ToString(),
                                Category = reader["Category"].ToString(),
                                PostImage = reader["PostImage"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            });
                        }
                    }
                }
            }

            return posts;
        }

    }
}