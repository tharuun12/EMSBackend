using System.ComponentModel.DataAnnotations;

namespace EMS.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string? Email { get; set; }

        public string? Otp { get; set; }

        [Required, MinLength(6)]
        public string? NewPassword { get; set; }

        [Required, Compare("NewPassword")]
        public string? ConfirmPassword { get; set; }
    }

}
