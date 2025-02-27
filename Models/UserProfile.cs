using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace NewsPortalApp.Models
{
    public class UserProfile
    {
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [StringLength(50, ErrorMessage = "Full Name cannot be longer than 50 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(50, ErrorMessage = "Email cannot be longer than 50 characters")]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [StringLength(256, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 256 characters")]
        public string Password { get; set; }

        [Display(Name = "Profile Image")]
        public IFormFile ProfileImage { get; set; }

        public string ProfileImagePath { get; set; }

        public bool IsGoogleAccount { get; set; }
    }
}