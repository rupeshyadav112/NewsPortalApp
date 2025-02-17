using System.ComponentModel.DataAnnotations;

namespace NewsPortalApp.Models
{
    public class UserProfile
    {
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; }

        [Display(Name = "Profile Image")]
        public IFormFile ProfileImage { get; set; }

        public string ProfileImagePath { get; set; }
    }
}