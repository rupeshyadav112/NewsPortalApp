using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.Models;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;


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

        [HttpGet]
        public async Task<IActionResult> Index()
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

            if (string.IsNullOrEmpty(model.FullName) && !string.IsNullOrEmpty(model.Username))
            {
                model.FullName = model.Username;
            }

            return View("Profile", model);
        }

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

            _logger.LogInformation("UpdateProfile called for UserId: {UserId}", userId);
            _logger.LogInformation("Received model: FullName={FullName}, Email={Email}, ProfileImage={ProfileImage}", model.FullName, model.Email, model.ProfileImage?.FileName);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid for user {UserId}: {Errors}", userId, ModelState.Values);
                TempData["Message"] = "Please fix the validation errors and try again.";
                TempData["IsSuccess"] = false;
                return View("Profile", model);
            }

            try
            {
                var existingProfile = await GetUserProfileAsync(userId);
                if (existingProfile == null)
                {
                    _logger.LogError("Existing profile not found for UserId: {UserId}", userId);
                    throw new Exception("User profile not found.");
                }

                string newImagePath = existingProfile.ProfileImagePath;

                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    _logger.LogInformation("New profile image upload attempt for user {UserId} with file: {FileName}", userId, model.ProfileImage.FileName);
                    newImagePath = await UploadImageAsync(model.ProfileImage);
                    _logger.LogInformation("New image uploaded successfully: {NewImagePath}", newImagePath);
                }
                else
                {
                    _logger.LogInformation("No new image uploaded, retaining existing path: {ExistingPath}", existingProfile.ProfileImagePath);
                }

                _logger.LogInformation("Setting model.ProfileImagePath to: {NewImagePath}", newImagePath);
                model.ProfileImagePath = newImagePath;

                if (string.IsNullOrEmpty(model.FullName) && !string.IsNullOrEmpty(existingProfile.Username))
                {
                    model.FullName = existingProfile.Username;
                }

                string passwordHash = null;
                if (!string.IsNullOrEmpty(model.Password) && !model.IsGoogleAccount)
                {
                    passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                    _logger.LogInformation("Password hash generated for user {UserId}", userId);
                }

                await UpdateUserProfileAsync(userId, model, passwordHash);
                _logger.LogInformation("Profile updated in database for user {UserId} with ProfileImagePath: {ProfileImagePath}", userId, model.ProfileImagePath);

                HttpContext.Session.SetString("Username", model.Username ?? existingProfile.Username);
                HttpContext.Session.SetString("FullName", model.FullName ?? existingProfile.FullName);
                HttpContext.Session.SetString("Email", model.Email ?? existingProfile.Email);
                HttpContext.Session.SetString("UserProfileImage", model.ProfileImagePath);
                _logger.LogInformation("Session updated with UserProfileImage: {UserProfileImage}", model.ProfileImagePath);

                TempData["Message"] = "Profile updated successfully!";
                TempData["IsSuccess"] = true;

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}: {ErrorMessage}", userId, ex.Message);
                TempData["Message"] = $"An error occurred while updating your profile: {ex.Message}";
                TempData["IsSuccess"] = false;
                return View("Profile", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            _logger.LogInformation("User signed out.");
            return RedirectToAction("Index", "Home");
        }

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
                TempData["Message"] = "An error occurred while deleting your account.";
                TempData["IsSuccess"] = false;
                return RedirectToAction("Index");
            }
        }

        private async Task<UserProfile> GetUserProfileAsync(string userId)
        {
            try
            {
                using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                string query = @"SELECT Username, FullName, Email, ProfileImagePath, IsGoogleAccount 
                                FROM Users 
                                WHERE UserID = @UserID";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserID", userId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var profile = new UserProfile
                    {
                        Username = reader["Username"]?.ToString(),
                        FullName = reader["FullName"]?.ToString(),
                        Email = reader["Email"]?.ToString(),
                        ProfileImagePath = reader["ProfileImagePath"]?.ToString() ?? "/images/Avatar.png",
                        IsGoogleAccount = reader.GetBoolean(reader.GetOrdinal("IsGoogleAccount"))
                    };
                    return profile;
                }

                _logger.LogWarning("No user found in database for UserId: {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching profile for user {UserId}", userId);
                throw;
            }
        }

        private async Task UpdateUserProfileAsync(string userId, UserProfile model, string passwordHash)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();
            _logger.LogInformation("Updating database for UserId: {UserId} with ProfileImagePath: {ProfileImagePath}", userId, model.ProfileImagePath);

            string query = @"UPDATE Users 
                            SET Username = @Username,
                                FullName = @FullName,
                                Email = @Email,
                                ProfileImagePath = @ProfileImagePath,
                                Password = COALESCE(@Password, Password)
                            WHERE UserID = @UserID";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserID", userId);
            command.Parameters.AddWithValue("@Username", model.Username ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FullName", model.FullName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Email", model.Email ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ProfileImagePath", model.ProfileImagePath ?? (object)DBNull.Value); // यहाँ सही वैल्यू पास होनी चाहिए
            command.Parameters.AddWithValue("@Password", (object)passwordHash ?? DBNull.Value);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Database update completed, rows affected: {RowsAffected}", rowsAffected);
            if (rowsAffected == 0)
            {
                _logger.LogWarning("No rows affected while updating profile for user {UserId}. Check UserID or data mismatch.", userId);
                throw new Exception("Profile update failed: No rows affected. Check UserID or data mismatch.");
            }
        }

        private async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file uploaded in UploadImageAsync.");
                throw new ArgumentException("No file uploaded.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                _logger.LogWarning("File size exceeds 5MB: {FileSize}", file.Length);
                throw new ArgumentException("File size must be less than 5MB.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                _logger.LogWarning("Invalid file extension: {Extension}", fileExtension);
                throw new ArgumentException("Only JPG, JPEG, PNG, and WEBP files are allowed.");
            }

            var uploadsPath = Path.Combine(_env.WebRootPath, "Uploads", "profile-images"); // Capital U for consistency
            if (!Directory.Exists(uploadsPath))
            {
                _logger.LogInformation("Creating directory: {UploadsPath}", uploadsPath);
                Directory.CreateDirectory(uploadsPath);
            }

            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            _logger.LogInformation("Saving image to: {FilePath}", filePath);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string newPath = $"/Uploads/profile-images/{fileName}"; // Capital U for consistency
            _logger.LogInformation("Image saved successfully at: {NewPath}", newPath);
            return newPath;
        }

        private async Task DeleteUserAsync(string userId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();

            string query = "DELETE FROM Users WHERE UserID = @UserID";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserID", userId);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
            {
                _logger.LogWarning("No rows affected while deleting user {UserId}", userId);
                throw new Exception("Account deletion failed: User not found.");
            }
        }
    }
}