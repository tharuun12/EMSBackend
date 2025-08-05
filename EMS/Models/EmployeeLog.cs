using System.ComponentModel.DataAnnotations;

namespace EMS.Models
{
    public class EmployeeLog
    {

        [Key]
        public int EmployeeIdLog { get; set; }

        public int EmployeeId { get; set; }

        [Display(Name = "Full Name")]
        public string? FullName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string? PhoneNumber { get; set; }

        public string? Role { get; set; }

        public bool IsActive { get; set; } = false;

        public string? RoleID { get; set; }

        public int DepartmentId { get; set; }

        public int? ManagerId { get; set; }

        public string? UserId { get; set; }

        public int LeaveBalance { get; set; } = 20;

        public string? Operation { get; set; }
        public DateTime? TimeStamp { get; set; }
    }
}
