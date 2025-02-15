using System.ComponentModel.DataAnnotations;

namespace NewsPortalApp.Models
{
    public class SignUpViewModel
    {
        [Required]
        [StringLength(20, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9]+$",
            ErrorMessage = "Username must be alphanumeric")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, and number")]
        public string Password { get; set; }
    }
}
