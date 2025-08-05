namespace EMS.Models
{
    public class UserActivityLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; } 
        public string? UrlAccessed { get; set; }
        public string? ActionName { get; set; }
        public string? ControllerName { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime AccessedAt { get; set; }
    }

}
