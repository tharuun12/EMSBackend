using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EMS.Web.Controllers
{
    [Route("manager/")]
    [ApiController]
    public class ManagerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<Users> _userManager;

        public ManagerController(
            AppDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<Users> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        private static int CalculateBusinessDays(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate) return 0;

            int businessDays = 0;
            DateTime current = startDate;

            while (current <= endDate)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
                current = current.AddDays(1);
            }

            return businessDays;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetManagerProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.UserId == userId);

            if (employee == null)
                return NotFound("Employee not found for current user.");

            return Ok(employee);
        }

        [HttpGet("approve-list")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetPendingApprovals()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var manager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId);

            if (manager == null)
                return NotFound("Manager not found.");

            var teamEmployeeIds = await _context.Employees
                .Where(e => e.ManagerId == manager.EmployeeId)
                .Select(e => e.EmployeeId)
                .ToListAsync();

            var leaves = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Status == "Pending" && teamEmployeeIds.Contains(l.EmployeeId))
                .ToListAsync();

            return Ok(leaves);
        }

        [HttpGet("approvals/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetApprovalDetails(int id)
        {
            var leave = await _context.LeaveRequests
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(l => l.LeaveRequestId == id);

            if (leave == null)
                return NotFound();

            return Ok(leave);
        }

        [HttpPost("approvals/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ApproveOrRejectLeave(int id, [FromQuery] string status)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave == null)
                return NotFound();

            leave.Status = status;

            if (status == "Approved")
            {
                var balance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(b => b.EmployeeId == leave.EmployeeId);

                if (balance != null)
                {
                    int days = CalculateBusinessDays(leave.StartDate, leave.EndDate) + 1;

                    if (balance.LeavesTaken + days > balance.TotalLeaves)
                    {
                        return BadRequest("Insufficient leave balance.");
                    }

                    balance.LeavesTaken += days;
                    await _context.SaveChangesAsync();
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Leave status updated successfully." });
        }

        [HttpGet("subordinates")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetSubordinates(int id)
        {
            var user = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == id);
            var userId = user.UserId;

            var manager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId);

            if (manager == null)
                return NotFound("Manager record not found.");

            var subordinates = await _context.Employees
                .Where(e => e.ManagerId == manager.EmployeeId)
                .ToListAsync();

            return Ok(subordinates);
        }
    }
}
