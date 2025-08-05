public class UserActivity
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    public string? ActivityType { get; set; } 

    public string? Description { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
