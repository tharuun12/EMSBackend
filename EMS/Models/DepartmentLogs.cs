using System.ComponentModel.DataAnnotations;

namespace EMS.Models
{
    public class DepartmentLogs
    {
        [Key]
        public int DepartmentLogId { get; set; }
        public int DepartmentId { get; set; }

        [Display(Name = "Department Name")]
        public string? DepartmentName { get; set; }

        public int? ManagerId { get; set; }

        public string? Operation { get; set; }
        public DateTime? TimeStamp { get; set; }

    }
}
