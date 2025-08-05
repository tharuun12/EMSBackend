using System.ComponentModel.DataAnnotations;

namespace EMS.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string? Email { get; set; }
    }

}
