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
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await GetUserProfileAsync(userId);
            return View("Profile", model);
        }

        // POST: Profile/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfile model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                if (model.ProfileImage != null)
                {
                    model.ProfileImagePath = await UploadImageAsync(model.ProfileImage);
                }

                await UpdateUserProfileAsync(userId, model);

                TempData["Message"] = "Profile updated successfully!";
                TempData["IsSuccess"] = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                TempData["Message"] = "An error occurred while updating your profile. Please try again.";
                TempData["IsSuccess"] = false;
            }

            return RedirectToAction("Index");
        }

        // POST: Profile/DeleteAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                await DeleteUserAsync(userId);
                return await Logout();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account for user {UserId}", userId);
                TempData["Message"] = "An error occurred while deleting your account. Please try again.";
                TempData["IsSuccess"] = false;
                return RedirectToAction("Index");
            }
        }

        // GET: Profile/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
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
                    profile.ProfileImagePath = reader["ProfileImagePath"]?.ToString();
                    
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching profile for user {UserId}", userId);
            }

            return profile;
        }

        private async Task UpdateUserProfileAsync(string userId, UserProfile model)
        {
            try
            {
                using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                string query = @"UPDATE Users 
                               SET Username = @Username,
                                   FullName = @FullName, 
                                   Email = @Email, 
                                   ProfileImagePath = @ProfileImagePath 
                               WHERE UserID = @UserID";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserID", userId);
                command.Parameters.AddWithValue("@Username", model.Username);
                command.Parameters.AddWithValue("@FullName", model.FullName);
                command.Parameters.AddWithValue("@Email", model.Email);
                command.Parameters.AddWithValue("@ProfileImagePath", model.ProfileImagePath ?? (object)DBNull.Value);

                await command.ExecuteNonQueryAsync();
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

            // Validate file size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
            {
                throw new ArgumentException("File size must be less than 5MB.");
            }

            // Validate file extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new ArgumentException("Only JPG, JPEG, and PNG files are allowed.");
            }

            // Create upload directory if it doesn't exist
            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "profile-images");
            Directory.CreateDirectory(uploadsPath);

            // Generate unique file name
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Save file
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

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                throw;
            }
        }
    }
}