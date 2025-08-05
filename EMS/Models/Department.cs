using EMS.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.Models
{
    public class Department
    {
        [Key]
        public int DepartmentId { get; set; }

        [Required]
        [Display(Name = "Department Name")]
        public string? DepartmentName { get; set; }

        public int? ManagerId { get; set; }
        public string? ManagerName { get; set; }


        [ForeignKey("ManagerId")]
        public Employee? Manager { get; set; }

        public ICollection<Employee>? Employees { get; set; }
    }
}
