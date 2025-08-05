using System.ComponentModel.DataAnnotations;

namespace EMS.ViewModels
{
    public class EmployeeViewModel
    {
        [Required, StringLength(100, MinimumLength = 3)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string PhoneNumber { get; set; }

        public string Role { get; set; }

        [Required]
        public bool IsActive { get; set; }

        [Display(Name = "Department")]
        [Required(ErrorMessage = "Please select a department.")]
        public int DepartmentId { get; set; }
        public string? DepartmentName { get; set; }

        [Display(Name = "Manager")]
        public int? ManagerId { get; set; }

        [Range(0, 365, ErrorMessage = "Leave balance must be between 0 and 365.")]
        public int LeaveBalance { get; set; } = 20;
        public string? ManagerName { get; set; }
    }
}
