using EMS.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.Repositories.Implementations
{
    public class UserActivityService : IUserActivityService
    {
        private readonly AppDbContext _context;

        public UserActivityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task LogActivityAsync(string userId, string action, HttpContext httpContext)
        {
            var activity = new UserActivity
            {
                UserId = userId,
                ActivityType = action,
                Timestamp = DateTime.UtcNow
            };

            _context.UserActivities.Add(activity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<UserActivity>> GetActivitiesByUserAsync(string userId)
        {
            return await _context.UserActivities
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        Task IUserActivityService.LogActivityAsync(string userId, string userName, string activityType, string description)
        {
            throw new NotImplementedException();
        }
    }


}
