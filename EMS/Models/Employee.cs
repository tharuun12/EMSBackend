using EMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.Models
{
    public class Employee
    {

        [Key]
        public int EmployeeId { get; set; }

        [Required, StringLength(100, MinimumLength = 3)]
        [Display(Name = "Full Name")]
        public string? FullName { get; set; }

        [Required, EmailAddress]
        public string? Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string? PhoneNumber { get; set; }

        [Required]
        public string? Role { get; set; }

        [Required]
        public bool IsActive { get; set; } = false;

        [ForeignKey("RoleId")]
        public string? RoleID { get; set; }

        [Required]
        public int DepartmentId { get; set; }

        [ForeignKey("DepartmentId")]
        public Department? Department { get; set; }

        [Display(Name = "Manager")]
        public int? ManagerId { get; set; } 

        [ForeignKey("ManagerId")]
        public Employee? Manager { get; set; } 

        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public Users? User { get; set; }

        public ICollection<Employee>? Subordinates { get; set; } 

        public int LeaveBalance { get; set; } = 20;
    }
}
