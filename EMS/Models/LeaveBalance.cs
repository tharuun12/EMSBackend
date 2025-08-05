using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.Models
{
    public class LeaveBalance
    {
        [Key]
        public int LeaveBalanceId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Required]
        public int TotalLeaves { get; set; } = 20;

        [Required]
        public int LeavesTaken { get; set; } = 0;

        [NotMapped]
        public int RemainingLeaves => TotalLeaves - LeavesTaken;

        public int total { get; set;  }
    }
}
