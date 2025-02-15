using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

        // GET: /Account/SignUp
        public IActionResult SignUp()
        {
            return View();
        }

        // POST: /Account/SignUp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignUp(SignUpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!ValidateUserInput(model))
            {
                return View(model);
            }

            try
            {
                using (var connection = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection")))
                {
                    connection.Open();

                    // Check existing user
                    var checkQuery = @"SELECT COUNT(*) FROM Users 
                                     WHERE Username = @Username OR Email = @Email";

                    using (var checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Username", model.Username);
                        checkCmd.Parameters.AddWithValue("@Email", model.Email);

                        var exists = (int)checkCmd.ExecuteScalar();
                        if (exists > 0)
                        {
                            ModelState.AddModelError("", "Username or email already exists");
                            return View(model);
                        }
                    }

                    // Insert new user
                    var insertQuery = @"INSERT INTO Users 
                                      (Username, Email, Password, FullName) 
                                      VALUES (@Username, @Email, @Password, @FullName)";

                    using (var insertCmd = new SqlCommand(insertQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@Username", model.Username);
                        insertCmd.Parameters.AddWithValue("@Email", model.Email);
                        insertCmd.Parameters.AddWithValue("@Password", HashPassword(model.Password));
                        insertCmd.Parameters.AddWithValue("@FullName", model.Username);

                        insertCmd.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Registration successful! Please login.";
                return RedirectToAction("SignIn");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Registration failed. Please try again.");
                // Log the error
                Console.WriteLine($"Registration Error: {ex}");
                return View(model);
            }
        }

        // GET: /Account/Logout
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

        private bool ValidateUserInput(SignUpViewModel model)
        {
            // Additional validation if needed
            return true;
        }
    }
}