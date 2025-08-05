public interface IUserActivityService
{
    Task LogActivityAsync(string userId, string userName, string activityType, string description);
}
