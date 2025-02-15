using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NewsPortalApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;

namespace NewsPortalApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET: /Account/SignIn
        public IActionResult SignIn()
        {
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: /Account/SignIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignIn(SignInViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (model.Email == "ryadav943@rku.ac.in" && model.Password == "Admin")
            {
                SetAdminSession();
                return RedirectToAction("Dashboard", "Home");
            }

            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                conn.Open();
                string query = @"
    SELECT UserID, Username, FullName, Email, ProfileImagePath, Password 
    FROM Users 
    WHERE Email = @Email";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", model.Email);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && reader["Password"].ToString() == HashPassword(model.Password))
                        {
                            SetUserSession(reader);
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }
            }
            ModelState.AddModelError("", "Invalid credentials");
            return View(model);
        }

        // GET: /Account/SignUp
        public IActionResult SignUp() => View();

        // POST: /Account/SignUp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignUp(SignUpViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                conn.Open();
                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username OR Email = @Email";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Username", model.Username);
                    checkCmd.Parameters.AddWithValue("@Email", model.Email);
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        ModelState.AddModelError("", "Username or email already exists");
                        return View(model);
                    }
                }

                string insertQuery = @"
                    INSERT INTO Users (Username, Email, Password, FullName, ProfileImagePath) 
                    VALUES (@Username, @Email, @Password, @FullName, @ProfileImagePath)";
                using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@Username", model.Username);
                    insertCmd.Parameters.AddWithValue("@Email", model.Email);
                    insertCmd.Parameters.AddWithValue("@Password", HashPassword(model.Password));
                    insertCmd.Parameters.AddWithValue("@FullName", model.Username);
                    insertCmd.Parameters.AddWithValue("@ProfileImagePath", "~/images/avatar.png");
                    insertCmd.ExecuteNonQuery();
                }
            }
            TempData["SuccessMessage"] = "Registration successful! Please login.";
            return RedirectToAction("SignIn");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("SignIn");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private void SetAdminSession()
        {
            HttpContext.Session.SetInt32("UserId", 1);
            HttpContext.Session.SetString("UserEmail", "ryadav943@rku.ac.in");
            HttpContext.Session.SetString("Username", "Admin");
            HttpContext.Session.SetString("FullName", "Administrator");
            HttpContext.Session.SetString("UserProfileImage", "~/images/admin.png");
        }

        private void SetUserSession(SqlDataReader reader)
        {
            HttpContext.Session.SetInt32("UserId", reader.GetInt32(reader.GetOrdinal("UserID")));
            HttpContext.Session.SetString("Username", reader["Username"].ToString());
            HttpContext.Session.SetString("FullName", reader["FullName"].ToString());
            HttpContext.Session.SetString("Email", reader["Email"].ToString());

            // ProfileImagePath कॉलम सुरक्षित रूप से चेक करें
            if (reader.HasColumn("ProfileImagePath") && !reader.IsDBNull(reader.GetOrdinal("ProfileImagePath")))
            {
                HttpContext.Session.SetString("UserProfileImage", reader["ProfileImagePath"].ToString());
            }
            else
            {
                HttpContext.Session.SetString("UserProfileImage", "~/images/avatar.png");
            }
        }
    }

    // SQL DataReader Extension Method
    public static class SqlDataReaderExtensions
    {
        public static bool HasColumn(this SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
