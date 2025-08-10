using EMS.Models;
using EMS.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.Web.ApiControllers
{
    [Route("/Activity")]
    [ApiController]
    public class ActivityController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ActivityController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Activity/employees
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("employees")]
        public async Task<IActionResult> GetEmployees()
        {
            var employees = await _context.Employees.ToListAsync();
            return Ok(employees);
        }

        // GET: /Activity/login-history/{userId}
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("login-history/{Id}")]
        public async Task<IActionResult> GetLoginHistory(int Id)
        {
            var user = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == Id);
            var userId = user?.UserId;
            if (string.IsNullOrEmpty(userId))
                return BadRequest("UserId is required");

            var logins = await _context.LoginActivityLogs
                .Where(log => log.userId == userId)
                .OrderByDescending(log => log.LoginTime)
                .ToListAsync();

            return Ok(new
            {
                userId,
                logs = logins
            });
        }

        // GET: /Activity/recent-activity/{userId}
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("recent-activity/")]
        public async Task<IActionResult> GetRecentActivity(int Id)
        {
            var user = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == Id);
            var userId = user?.UserId;
            if (string.IsNullOrEmpty(userId))
                return BadRequest("UserId is required");

            var activities = await _context.UserActivityLogs
                .Where(act => act.UserId == userId)
                .OrderByDescending(act => act.AccessedAt)
                .ToListAsync();

            return Ok(new
            {
                userId,
                activities
            });
        }
    }
}
