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
using System.Net.Mail;
using System.Net;

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
                        TempData["ErrorMessage"] = "Password is incorrect. Please try again.";
                        return View(model);
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Email is incorrect. Please check your email address.";
                    return View(model);
                }
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

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please enter a valid email address.";
                return View(model);
            }

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                // Check if email exists in the database and get UserId
                string query = "SELECT UserID FROM Users WHERE Email = @Email";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Email", model.Email);
                var userIdObj = await cmd.ExecuteScalarAsync();

                if (userIdObj == null)
                {
                    TempData["ErrorMessage"] = "No account found with this email address.";
                    return View(model);
                }

                int userId = (int)userIdObj;

                // Generate a unique token
                string resetToken = Guid.NewGuid().ToString();
                DateTime expiryDate = DateTime.UtcNow.AddHours(1); // Token valid for 1 hour

                // Store the token in the database
                string insertTokenQuery = @"
                    INSERT INTO PasswordResetTokens (UserId, Token, ExpiryDate, IsUsed)
                    VALUES (@UserId, @Token, @ExpiryDate, @IsUsed)";
                using var tokenCmd = new SqlCommand(insertTokenQuery, conn);
                tokenCmd.Parameters.AddWithValue("@UserId", userId);
                tokenCmd.Parameters.AddWithValue("@Token", resetToken);
                tokenCmd.Parameters.AddWithValue("@ExpiryDate", expiryDate);
                tokenCmd.Parameters.AddWithValue("@IsUsed", false);
                await tokenCmd.ExecuteNonQueryAsync();

                // Send email with reset link
                await SendPasswordResetEmail(model.Email, resetToken);

                TempData["SuccessMessage"] = "A password reset link has been sent to your email.";
                return RedirectToAction("ForgotPassword");
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = $"A database error occurred: {ex.Message}";
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                return View(model);
            }
        }

        // GET: /Account/ResetPassword?token={token}
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Invalid or missing reset token.";
                return RedirectToAction("SignIn");
            }

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                // Validate the token
                string query = @"
                    SELECT UserId, ExpiryDate, IsUsed 
                    FROM PasswordResetTokens 
                    WHERE Token = @Token";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Token", token);
                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    TempData["ErrorMessage"] = "Invalid reset token.";
                    return RedirectToAction("SignIn");
                }

                bool isUsed = reader.GetBoolean(reader.GetOrdinal("IsUsed"));
                DateTime expiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate"));

                if (isUsed)
                {
                    TempData["ErrorMessage"] = "This reset token has already been used.";
                    return RedirectToAction("SignIn");
                }

                if (expiryDate < DateTime.UtcNow)
                {
                    TempData["ErrorMessage"] = "This reset token has expired.";
                    return RedirectToAction("SignIn");
                }

                // Token is valid, pass it to the view
                var model = new ResetPasswordViewModel { Token = token };
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return RedirectToAction("SignIn");
            }
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
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

                // Validate the token again
                string query = @"
                    SELECT UserId, ExpiryDate, IsUsed 
                    FROM PasswordResetTokens 
                    WHERE Token = @Token";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Token", model.Token);
                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    TempData["ErrorMessage"] = "Invalid reset token.";
                    return RedirectToAction("SignIn");
                }

                int userId = reader.GetInt32(reader.GetOrdinal("UserId"));
                bool isUsed = reader.GetBoolean(reader.GetOrdinal("IsUsed"));
                DateTime expiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate"));

                if (isUsed)
                {
                    TempData["ErrorMessage"] = "This reset token has already been used.";
                    return RedirectToAction("SignIn");
                }

                if (expiryDate < DateTime.UtcNow)
                {
                    TempData["ErrorMessage"] = "This reset token has expired.";
                    return RedirectToAction("SignIn");
                }

                // Mark the token as used
                await reader.CloseAsync();
                string updateTokenQuery = @"
                    UPDATE PasswordResetTokens 
                    SET IsUsed = 1 
                    WHERE Token = @Token";
                using var updateTokenCmd = new SqlCommand(updateTokenQuery, conn);
                updateTokenCmd.Parameters.AddWithValue("@Token", model.Token);
                await updateTokenCmd.ExecuteNonQueryAsync();

                // Update the user's password
                string updatePasswordQuery = @"
                    UPDATE Users 
                    SET Password = @Password 
                    WHERE UserID = @UserId";
                using var updatePasswordCmd = new SqlCommand(updatePasswordQuery, conn);
                updatePasswordCmd.Parameters.AddWithValue("@Password", HashPassword(model.NewPassword));
                updatePasswordCmd.Parameters.AddWithValue("@UserId", userId);
                await updatePasswordCmd.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = "Your password has been successfully reset. Please sign in with your new password.";
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
            var profileImageUrl = result.Principal.FindFirst("picture")?.Value; // Google profile image URL

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
                // New user, insert into database
                string insertQuery = @"
                    INSERT INTO Users (Username, Email, FullName, ProfileImagePath, CreatedAt, IsGoogleAccount) 
                    VALUES (@Username, @Email, @FullName, @ProfileImagePath, @CreatedAt, @IsGoogleAccount)";
                using var insertCmd = new SqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("@Username", email.Split('@')[0]);
                insertCmd.Parameters.AddWithValue("@Email", email);
                insertCmd.Parameters.AddWithValue("@FullName", fullName ?? email.Split('@')[0]);
                insertCmd.Parameters.AddWithValue("@ProfileImagePath", (object)profileImageUrl ?? DBNull.Value); // Store Google image URL
                insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                insertCmd.Parameters.AddWithValue("@IsGoogleAccount", true);
                await insertCmd.ExecuteNonQueryAsync();

                // Set session for the new user
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
        }

        // Helper Methods
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private async Task SendPasswordResetEmail(string email, string resetToken)
        {
            var resetLink = Url.Action("ResetPassword", "Account", new { token = resetToken }, protocol: Request.Scheme);
            var fromAddress = new MailAddress("ramuydvji112@gmail.com", "NewsPortal_App");
            var toAddress = new MailAddress(email);
            const string subject = "Password Reset Request";
            string body = $"Please reset your password by clicking here: <a href='{resetLink}'>Reset Your Password</a>";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("ramuydvji112@gmail.com", "uqxo bdbg apuc qgvn")
            };

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message);
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
