namespace EMS.Repositories.Interfaces
{
    public interface IUserActivityService
    {
        Task LogActivityAsync(string userId, string action, HttpContext httpContext);
        Task<List<UserActivity>> GetActivitiesByUserAsync(string userId);
    }

}
