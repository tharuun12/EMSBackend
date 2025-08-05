using System.ComponentModel.DataAnnotations;

namespace EMS.ViewModels
{
    public class VerifyOtpViewModel
    {
        public string? Email { get; set; }
        public string? Otp { get; set; }
        public string? ExpectedOtp { get; set; }
        public string? OtpExpiry { get; set; }
    }


}
