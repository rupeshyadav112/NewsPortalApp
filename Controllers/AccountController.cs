using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Google;


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
        public async Task<IActionResult> SignIn(SignInViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill all required fields correctly.";
                return View(model);
            }

            try
            {
                // Admin credentials check
                if (model.Email == "ryadav943@rku.ac.in" && model.Password == "Admin")
                {
                    await SetAdminSessionAsync();
                    TempData["SuccessMessage"] = "Admin login successful!";
                    return RedirectToAction("Index", "Dashboard");
                }

                // Database se user check
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                string query = @"
                    SELECT UserID, Username, FullName, Email, ProfileImagePath, Password 
                    FROM Users 
                    WHERE Email = @Email";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Email", model.Email);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    string storedPassword = reader["Password"]?.ToString();
                    if (!string.IsNullOrEmpty(storedPassword) && storedPassword == HashPassword(model.Password))
                    {
                        await SetUserSessionAsync(reader);
                        TempData["SuccessMessage"] = "Welcome back! Login successful.";
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Invalid email or password.";
                        return View(model);
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Invalid email or password.";
                    return View(model);
                }
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = "Database error occurred. Please try again later.";
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return View(model);
            }
        }

        // GET: /Account/SignUp
        [HttpGet]
        public IActionResult SignUp()
        {
            return View();
        }

        // POST: /Account/SignUp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(SignUpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill all required fields correctly.";
                return View(model);
            }

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                // Check for existing user
                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username OR Email = @Email";
                using var checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@Username", model.Username);
                checkCmd.Parameters.AddWithValue("@Email", model.Email);
                int existingCount = (int)await checkCmd.ExecuteScalarAsync();
                if (existingCount > 0)
                {
                    TempData["ErrorMessage"] = "Username or email already exists.";
                    return View(model);
                }

                // Insert new user with corrected ProfileImagePath
                string insertQuery = @"
                    INSERT INTO Users (Username, Email, Password, FullName, ProfileImagePath, CreatedAt) 
                    VALUES (@Username, @Email, @Password, @FullName, @ProfileImagePath, @CreatedAt)";
                using var insertCmd = new SqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("@Username", model.Username);
                insertCmd.Parameters.AddWithValue("@Email", model.Email);
                insertCmd.Parameters.AddWithValue("@Password", HashPassword(model.Password));
                insertCmd.Parameters.AddWithValue("@FullName", model.Username);
                insertCmd.Parameters.AddWithValue("@ProfileImagePath", "/images/Avatar.png");
                insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                await insertCmd.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = "Registration successful! Please sign in to continue.";
                return RedirectToAction("SignIn");
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = "A database error occurred. Please try again later.";
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return View(model);
            }
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("SignIn");
        }

        // GET: /Account/GoogleLogin
        [HttpGet]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleLoginCallback") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // GET: /Account/GoogleLoginCallback
        [HttpGet]
        public async Task<IActionResult> GoogleLoginCallback()
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Google authentication failed.";
                return RedirectToAction("SignIn");
            }

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var fullName = result.Principal.FindFirst(ClaimTypes.Name)?.Value;
            var googleId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var profileImageUrl = result.Principal.FindFirst("picture")?.Value; // Google प्रोफाइल इमेज URL

            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Email not provided by Google.";
                return RedirectToAction("SignIn");
            }

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            string query = "SELECT UserID, Username, FullName, Email, ProfileImagePath, Password, IsGoogleAccount FROM Users WHERE Email = @Email";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Email", email);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                await SetUserSessionAsync(reader);
            }
            else
            {
                // नया यूज़र, डेटाबेस में डालें
                string insertQuery = @"
            INSERT INTO Users (Username, Email, FullName, ProfileImagePath, CreatedAt, IsGoogleAccount) 
            VALUES (@Username, @Email, @FullName, @ProfileImagePath, @CreatedAt, @IsGoogleAccount)";
                using var insertCmd = new SqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("@Username", email.Split('@')[0]);
                insertCmd.Parameters.AddWithValue("@Email", email);
                insertCmd.Parameters.AddWithValue("@FullName", fullName ?? email.Split('@')[0]);
                insertCmd.Parameters.AddWithValue("@ProfileImagePath", (object)profileImageUrl ?? DBNull.Value); // Google इमेज URL स्टोर करें
                insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                insertCmd.Parameters.AddWithValue("@IsGoogleAccount", true);
                await insertCmd.ExecuteNonQueryAsync();

                // नया यूज़र सेशन में सेट करें
                using var newCmd = new SqlCommand(query, conn);
                newCmd.Parameters.AddWithValue("@Email", email);
                using var newReader = await newCmd.ExecuteReaderAsync();
                if (await newReader.ReadAsync())
                {
                    await SetUserSessionAsync(newReader);
                }
            }

            TempData["SuccessMessage"] = "Successfully logged in with Google!";
            return RedirectToAction("Index", "Home");
        } // Helper Methods

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private async Task SetAdminSessionAsync()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "Admin"),
                new Claim(ClaimTypes.Email, "ryadav943@rku.ac.in"),
                new Claim("FullName", "Administrator"),
                new Claim("ProfileImagePath", "/images/admin.png")
            };

            var identity = new ClaimsIdentity(claims, "custom");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("custom", principal);

            HttpContext.Session.SetInt32("UserId", 1);
            HttpContext.Session.SetString("UserEmail", "ryadav943@rku.ac.in");
            HttpContext.Session.SetString("Username", "Admin");
            HttpContext.Session.SetString("FullName", "Administrator");
            HttpContext.Session.SetString("UserProfileImage", "/images/admin.png");
        }

        private async Task SetUserSessionAsync(SqlDataReader reader)
        {
            var userId = reader.GetInt32(reader.GetOrdinal("UserID")).ToString();
            var username = reader["Username"]?.ToString() ?? string.Empty;
            var fullName = reader["FullName"]?.ToString() ?? string.Empty;
            var email = reader["Email"]?.ToString() ?? string.Empty;
            var profileImagePath = reader.HasColumn("ProfileImagePath") && !reader.IsDBNull(reader.GetOrdinal("ProfileImagePath"))
                ? reader["ProfileImagePath"].ToString()
                : "/images/Avatar.png";

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Email, email),
                new Claim("FullName", fullName),
                new Claim("ProfileImagePath", profileImagePath)
            };

            var identity = new ClaimsIdentity(claims, "custom");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("custom", principal);

            HttpContext.Session.SetInt32("UserId", int.Parse(userId));
            HttpContext.Session.SetString("Username", username);
            HttpContext.Session.SetString("FullName", fullName);
            HttpContext.Session.SetString("Email", email);
            HttpContext.Session.SetString("UserProfileImage", profileImagePath);
        }
    }

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
