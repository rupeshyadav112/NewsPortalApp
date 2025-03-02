
﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.Models;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
namespace NewsPortalApp.Controllers
{
    public class ProfileController : Controller
    {
        private readonly IConfiguration _configuration;

        public ProfileController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        [Route("/Profile")]
        public async Task<IActionResult> UpdateProfile()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            try
            {
                int userId = HttpContext.Session.GetInt32("UserId").Value;
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                string query = @"
                    SELECT Username, FullName, Email, ProfileImagePath, IsGoogleAccount 
                    FROM Users 
                    WHERE UserID = @UserId";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var model = new UserProfile
                    {
                        Username = reader["Username"].ToString(),
                        FullName = reader["FullName"]?.ToString(),
                        Email = reader["Email"].ToString(),
                        ProfileImagePath = reader["ProfileImagePath"]?.ToString(),
                        IsGoogleAccount = reader.GetBoolean(reader.GetOrdinal("IsGoogleAccount"))
                    };

                    if (model.IsGoogleAccount && string.IsNullOrEmpty(model.ProfileImagePath))
                    {
                        model.ProfileImagePath = HttpContext.Session.GetString("GoogleProfileImage");
                    }

                    return View("Profile", model);
                }
                else
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("SignIn", "Account");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading your profile.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfile model)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Please correct the errors in the form.";
                TempData["IsSuccess"] = false;
                return View("Profile", model);
            }

            try
            {
                int userId = HttpContext.Session.GetInt32("UserId").Value;
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                // पहले डेटाबेस से मौजूदा प्रोफाइल इमेज लोड करें
                string currentProfileImagePath;
                string selectQuery = @"
                    SELECT ProfileImagePath 
                    FROM Users 
                    WHERE UserID = @UserId";
                using (var selectCmd = new SqlCommand(selectQuery, conn))
                {
                    selectCmd.Parameters.AddWithValue("@UserId", userId);
                    currentProfileImagePath = await selectCmd.ExecuteScalarAsync() as string;
                }

                // नई इमेज अपलोड की गई है या पुरानी इमेज रखनी है
                string profileImagePath = currentProfileImagePath; // डिफ़ॉल्ट रूप से पुरानी इमेज
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProfileImage.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProfileImage.CopyToAsync(fileStream);
                    }

                    profileImagePath = "/uploads/" + uniqueFileName; // नई इमेज का पाथ
                }

                // डेटाबेस में अपडेट करें
                string updateQuery = @"
                    UPDATE Users 
                    SET FullName = @FullName, 
                        Email = @Email, 
                        ProfileImagePath = @ProfileImagePath,
                        Password = CASE WHEN @Password IS NOT NULL THEN @Password ELSE Password END
                    WHERE UserID = @UserId";
                using var cmd = new SqlCommand(updateQuery, conn);
                cmd.Parameters.AddWithValue("@FullName", (object)model.FullName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Email", model.Email);
                cmd.Parameters.AddWithValue("@ProfileImagePath", (object)profileImagePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", userId);

                if (!string.IsNullOrEmpty(model.Password) && !model.IsGoogleAccount)
                {
                    cmd.Parameters.AddWithValue("@Password", HashPassword(model.Password));
                }
                else
                {
                    cmd.Parameters.AddWithValue("@Password", DBNull.Value);
                }

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    HttpContext.Session.SetString("FullName", model.FullName ?? model.Username);
                    HttpContext.Session.SetString("Email", model.Email);
                    HttpContext.Session.SetString("UserProfileImage", profileImagePath ?? "");

                    TempData["Message"] = "Profile updated successfully!";
                    TempData["IsSuccess"] = true;

                    // अपडेट के बाद मॉडल को ताज़ा डेटा से भरें
                    string refreshQuery = @"
                        SELECT Username, FullName, Email, ProfileImagePath, IsGoogleAccount 
                        FROM Users 
                        WHERE UserID = @UserId";
                    using var refreshCmd = new SqlCommand(refreshQuery, conn);
                    refreshCmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await refreshCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        model.Username = reader["Username"].ToString();
                        model.FullName = reader["FullName"]?.ToString();
                        model.Email = reader["Email"].ToString();
                        model.ProfileImagePath = reader["ProfileImagePath"]?.ToString();
                        model.IsGoogleAccount = reader.GetBoolean(reader.GetOrdinal("IsGoogleAccount"));

                        if (model.IsGoogleAccount && string.IsNullOrEmpty(model.ProfileImagePath))
                        {
                            model.ProfileImagePath = HttpContext.Session.GetString("GoogleProfileImage");
                        }
                    }

                    return View("Profile", model);
                }
                else
                {
                    TempData["Message"] = "Failed to update profile.";
                    TempData["IsSuccess"] = false;
                    return View("Profile", model);
                }
            }
            catch (SqlException ex)
            {
                TempData["Message"] = "A database error occurred. Please try again later.";
                TempData["IsSuccess"] = false;
                return View("Profile", model);
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["IsSuccess"] = false;
                return View("Profile", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            try
            {
                int userId = HttpContext.Session.GetInt32("UserId").Value;
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                string deleteQuery = "DELETE FROM Users WHERE UserID = @UserId";
                using var cmd = new SqlCommand(deleteQuery, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);

                await cmd.ExecuteNonQueryAsync();

                await HttpContext.SignOutAsync();
                HttpContext.Session.Clear();

                TempData["SuccessMessage"] = "Your account has been deleted successfully.";
                return RedirectToAction("SignIn", "Account");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting your account.";
                return RedirectToAction("UpdateProfile");
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            return RedirectToAction("Logout", "Account");
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
