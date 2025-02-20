using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NewsPortalApp.Models;
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
    [Route("[controller]")]
    public class ProfileController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<ProfileController> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public ProfileController(
            IWebHostEnvironment env,
            IConfiguration config,
            UserManager<IdentityUser> userManager,
            ILogger<ProfileController> logger)
        {
            _env = env;
            _config = config;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Profile
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await GetUserProfileAsync(userId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfile model)
        {
            if (!ModelState.IsValid)
            {
                return View("Profile", model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            var isGoogleAccount = await IsGoogleAccountAsync(userId);

            try
            {
                // Password Update
                if (!isGoogleAccount && !string.IsNullOrEmpty(model.Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var result = await _userManager.ResetPasswordAsync(user, token, model.Password);

                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("Password", error.Description);
                        }
                        return View("Profile", model);
                    }
                }

                // Profile Image Upload
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
                _logger.LogError(ex, "Error updating profile");
                TempData["Message"] = $"Error: {ex.Message}";
                TempData["IsSuccess"] = false;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            try
            {
                // Delete from ASP.NET Identity
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
                }

                // Delete from custom Users table
                await DeleteUserAsync(userId);

                return await Logout();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account deletion failed");
                TempData["Message"] = $"Delete failed: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        #region Helper Methods

        private async Task<UserProfile> GetUserProfileAsync(string userId)
        {
            var profile = new UserProfile();

            try
            {
                using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                const string query = @"
                    SELECT Username, FullName, Email, 
                           ProfileImagePath, IsGoogleAccount 
                    FROM Users 
                    WHERE UserID = @UserID";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserID", userId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    profile.Username = reader["Username"]?.ToString();
                    profile.FullName = reader["FullName"]?.ToString();
                    profile.Email = reader["Email"]?.ToString();
                    profile.ProfileImagePath = reader["ProfileImagePath"]?.ToString();
                    //profile.IsGoogleAccount = Convert.ToBoolean(reader["IsGoogleAccount"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching profile");
            }

            return profile;
        }

        private async Task UpdateUserProfileAsync(string userId, UserProfile model)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();

            const string query = @"
                UPDATE Users 
                SET Username = @Username,
                    FullName = @FullName, 
                    Email = @Email, 
                    ProfileImagePath = @ProfileImagePath 
                WHERE UserID = @UserID";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserID", userId);
            command.Parameters.AddWithValue("@Username", model.Username);
            command.Parameters.AddWithValue("@FullName", model.FullName);
            command.Parameters.AddWithValue("@Email", model.Email);
            command.Parameters.AddWithValue("@ProfileImagePath",
                string.IsNullOrEmpty(model.ProfileImagePath) ? DBNull.Value : model.ProfileImagePath);

            await command.ExecuteNonQueryAsync();
        }

        private async Task<bool> IsGoogleAccountAsync(string userId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();

            const string query = "SELECT IsGoogleAccount FROM Users WHERE UserID = @UserID";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserID", userId);

            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }

        private async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file selected");

            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException("File size exceeds 5MB limit");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("Invalid file type. Allowed: JPG, JPEG, PNG");

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
            using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();

            const string query = "DELETE FROM Users WHERE UserID = @UserID";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserID", userId);

            await command.ExecuteNonQueryAsync();
        }

        #endregion
    }
}























