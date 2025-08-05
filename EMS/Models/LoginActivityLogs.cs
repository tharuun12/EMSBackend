using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.Models
{
    public class LoginActivityLogs
    {
        public int Id { get; set; }
        public string? userId { get; set; }
        public int employeeId { get; set; }
        public DateTime LoginTime { get; set; } 
        public string? IpAddress { get; set; }
        public bool IsSuccessful { get; set; } = false;
        public string? Email { get; set; }
        public DateTime? LogoutTime { get; set; }

        [NotMapped]
        public TimeSpan? SessionDuration => LogoutTime.HasValue ? LogoutTime - LoginTime : null;

    }
}
