using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.Models;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NewsPortalApp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<ProfileController> logger)
        {
            _env = env;
            _config = config;
            _logger = logger;
        }

        // GET: Profile
        [HttpGet]
        public async Task<IActionResult> Index() // Changed from Index to Profile for consistency
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims.");
                return RedirectToAction("SignIn", "Account");
            }

            var model = await GetUserProfileAsync(userId);
            if (model == null)
            {
                _logger.LogError("User profile not found for UserId: {UserId}", userId);
                return NotFound("User profile not found.");
            }

            return View("Profile", model); // Explicitly targeting Profile.cshtml
        }

        // POST: Profile Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfile model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims during update.");
                return RedirectToAction("SignIn", "Account");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid for user {UserId}", userId);
                return View("Profile", model);
            }

            try
            {
                // Agar naya image upload kiya gaya hai, toh update karo
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    model.ProfileImagePath = await UploadImageAsync(model.ProfileImage);
                }
                else
                {
                    // Agar image upload nahi kiya, toh existing path ko retain karo
                    var existingProfile = await GetUserProfileAsync(userId);
                    if (existingProfile != null)
                    {
                        model.ProfileImagePath = existingProfile.ProfileImagePath;
                    }
                    else
                    {
                        model.ProfileImagePath = "/images/avatar.png"; // Fallback if no existing profile
                    }
                }

                // Password update agar provided hai
                string passwordHash = null;
                if (!string.IsNullOrEmpty(model.Password) && !model.IsGoogleAccount)
                {
                    passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                await UpdateUserProfileAsync(userId, model, passwordHash);

                TempData["Message"] = "Profile updated successfully!";
                TempData["IsSuccess"] = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                TempData["Message"] = "An error occurred while updating your profile. Please try again.";
                TempData["IsSuccess"] = false;
                return View("Profile", model);
            }

            return RedirectToAction("Profile");
        }

        // POST: Delete Account
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims during delete.");
                return RedirectToAction("SignIn", "Account");
            }

            try
            {
                await DeleteUserAsync(userId);
                _logger.LogInformation("User {UserId} successfully deleted their account.", userId);
                return await Logout();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account for user {UserId}", userId);
                TempData["Message"] = "An error occurred while deleting your account. Please try again.";
                TempData["IsSuccess"] = false;
                return RedirectToAction("Profile");
            }
        }

        // GET: Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            _logger.LogInformation("User signed out.");
            return RedirectToAction("Index", "Home");
        }

        // Helper Methods

        private async Task<UserProfile> GetUserProfileAsync(string userId)
        {
            var profile = new UserProfile();

            try
            {
                using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                string query = @"SELECT Username, FullName, Email, ProfileImagePath, IsGoogleAccount 
                               FROM Users 
                               WHERE UserID = @UserID";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserID", userId);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    profile.Username = reader["Username"]?.ToString();
                    profile.FullName = reader["FullName"]?.ToString();
                    profile.Email = reader["Email"]?.ToString();
                    profile.ProfileImagePath = reader["ProfileImagePath"]?.ToString() ?? "/images/avatar.png"; // Default only if null in DB
                    profile.IsGoogleAccount = Convert.ToBoolean(reader["IsGoogleAccount"]);
                }
                else
                {
                    _logger.LogWarning("No user found in database for UserId: {UserId}", userId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching profile for user {UserId}", userId);
                return null;
            }

            return profile;
        }

        private async Task UpdateUserProfileAsync(string userId, UserProfile model, string passwordHash = null)
        {
            try
            {
                using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                string query = @"UPDATE Users 
                               SET Username = @Username,
                                   FullName = @FullName, 
                                   Email = @Email, 
                                   ProfileImagePath = @ProfileImagePath,
                                   Password = ISNULL(@Password, Password)
                               WHERE UserID = @UserID";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserID", userId);
                command.Parameters.AddWithValue("@Username", model.Username ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@FullName", model.FullName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Email", model.Email ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ProfileImagePath", model.ProfileImagePath ?? (object)DBNull.Value); // Save uploaded path or retain existing
                command.Parameters.AddWithValue("@Password", (object)passwordHash ?? DBNull.Value);

                int rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No rows affected while updating profile for user {UserId}", userId);
                    throw new Exception("Profile update failed: User not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                throw;
            }
        }

        private async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file uploaded.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                throw new ArgumentException("File size must be less than 5MB.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new ArgumentException("Only JPG, JPEG, and PNG files are allowed.");
            }

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "profile-images");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/profile-images/{fileName}";
        }

        private async Task DeleteUserAsync(string userId)
        {
            try
            {
                using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                string query = "DELETE FROM Users WHERE UserID = @UserID";
                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserID", userId);

                int rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No rows affected while deleting user {UserId}", userId);
                    throw new Exception("Account deletion failed: User not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                throw;
            }
        }
    }
}