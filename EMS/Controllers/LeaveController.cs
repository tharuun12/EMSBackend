using EMS.Models;
using EMS.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EMS.Web.Controllers
{
    [Route("/leave")]
    [ApiController]
    public class LeaveController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LeaveController(AppDbContext context)
        {
            _context = context;
        }

        private static int CalculateBusinessDays(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate) return 0;

            int days = 0;
            while (startDate <= endDate)
            {
                if (startDate.DayOfWeek != DayOfWeek.Saturday && startDate.DayOfWeek != DayOfWeek.Sunday)
                    days++;
                startDate = startDate.AddDays(1);
            }
            return days;
        }

        public async Task<LeaveBalance?> EnsureLeaveBalanceExistsAsync(int employeeId, int leaveBalance)
        {
            var balance = await _context.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
            if (balance != null)
            {
                var remainingLeave = balance.TotalLeaves - balance.LeavesTaken;
                if (remainingLeave >= leaveBalance)
                {
                    return balance;
                }
                return null;
            }

            var emp = await _context.Employees.FindAsync(employeeId);
            if (emp == null) return null;

            var newBalance = new LeaveBalance
            {
                EmployeeId = employeeId,
                TotalLeaves = emp.LeaveBalance,
                LeavesTaken = 0
            };

            _context.LeaveBalances.Add(newBalance);
            await _context.SaveChangesAsync();

            return newBalance;
        }

        private async Task<bool> UpdateLeaveBalanceAsync(int employeeId, DateTime start, DateTime end, int LeaveRequested)
        {
            int days = CalculateBusinessDays(start, end);
            if (days <= 0) return false;

            var leaveBalance = await EnsureLeaveBalanceExistsAsync(employeeId, LeaveRequested);
            var employee = await _context.Employees.FindAsync(employeeId);

            if (leaveBalance == null || employee == null || employee.LeaveBalance < days)
                return false;

            leaveBalance.LeavesTaken += days;
            //employee.LeaveBalance = Math.Max(0, employee.LeaveBalance - days);
            //_context.LeaveBalances.Update(leaveBalance);

            _context.Employees.Update(employee);
            await _context.SaveChangesAsync();

            return true;
        }

        [Authorize]
        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] LeaveRequest leave)
        {
            if (leave.StartDate > leave.EndDate)
                return BadRequest("End date must be after start date.");

            int daysRequested = CalculateBusinessDays(leave.StartDate, leave.EndDate);
            if (daysRequested <= 0)
                return BadRequest("Invalid leave period.");

            var employee = await _context.Employees.FindAsync(leave.EmployeeId);
            if (employee == null)
                return NotFound("Employee not found.");

            //if (employee.LeaveBalance < daysRequested)
            //    return BadRequest($"Insufficient leave balance. Available: {employee.LeaveBalance}, Requested: {daysRequested}");

            leave.RequestDate = DateTime.UtcNow;
            leave.Status = string.IsNullOrEmpty(leave.Status) || leave.Status != "Approved" ? "Pending" : "Approved";
            var checkBalance = await EnsureLeaveBalanceExistsAsync(leave.EmployeeId, daysRequested);
            if (checkBalance == null)
            {
                var balance = await _context.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == leave.EmployeeId);
                var balanceLeave = balance?.TotalLeaves - balance?.LeavesTaken;
                if (balanceLeave < 0)
                {
                    balanceLeave = 0;
                }
                return BadRequest(new { message = $"Insufficient balance. Available leave: {balanceLeave}, Requested leave: {daysRequested}" });
            }

            _context.LeaveRequests.Add(leave);
            await _context.SaveChangesAsync();


            if (leave.Status == "Approved")
            {
                var updated = await UpdateLeaveBalanceAsync(leave.EmployeeId, leave.StartDate, leave.EndDate, daysRequested);
                if (!updated)
                    return StatusCode(500, "Failed to update leave balance.");

                return Ok("Leave applied and approved successfully.");
            }

            return Ok("Leave application submitted successfully.");
        }

        [Authorize]
        [HttpGet("my/{employeeId}")]
        public async Task<IActionResult> MyLeaves(int employeeId)
        {
            var leaves = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.EmployeeId == employeeId)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            return Ok(leaves);
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("pending")]
        public async Task<IActionResult> ApproveList()
        {
            var leaves = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Status == "Pending")
                .ToListAsync();

            return Ok(leaves);
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("manager-leave-approval")]
        public async Task<IActionResult> EmployeeLeaveList(int employeeId)
        {
            
            var userId = await _context.Employees
                .Where(e => e.EmployeeId == employeeId)
                .Select(e => e.UserId)
                .FirstOrDefaultAsync();

            if (userId == null)
                return NotFound("UserId not found for the given EmployeeId.");           

            var manager = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
            if (manager == null) return NotFound("Manager not found.");

            var teamIds = await _context.Employees
                .Where(e => e.ManagerId == manager.EmployeeId)
                .Select(e => e.EmployeeId)
                .ToListAsync();

            var leaves = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Status == "Pending" && teamIds.Contains(l.EmployeeId))
                .ToListAsync();

            return Ok(leaves);
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLeave(int id)
        {
            var leave = await _context.LeaveRequests
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(l => l.LeaveRequestId == id);

            if (leave == null) return NotFound();
            return Ok(leave);
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost("approved/{id}")]
        public async Task<IActionResult> ApproveLeave(int id, [FromBody] ApproveLeaveRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Status))
                return BadRequest("Status is required.");

            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave == null) return NotFound();

            string originalStatus = leave.Status;

            if (request.Status == "approved" && originalStatus != "Approved")
            {
                int daysRequested = CalculateBusinessDays(leave.StartDate, leave.EndDate);
                var employee = await _context.Employees.FindAsync(leave.EmployeeId);
                if (employee == null)
                    return NotFound("Employee not found.");

                //if (employee.LeaveBalance < daysRequested)
                //    return BadRequest($"Insufficient balance. Available: {employee.LeaveBalance}, Requested: {daysRequested}");

                var checkLeave = await EnsureLeaveBalanceExistsAsync(leave.EmployeeId, daysRequested);
                if (checkLeave == null)
                {
                    var balance = await _context.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == leave.EmployeeId);
                    var balanceLeave = balance?.TotalLeaves - balance?.LeavesTaken;
                    if (balanceLeave < 0)
                    {
                        balanceLeave = 0;
                    }

                    return BadRequest(new { message = $"Insufficient balance. Available: {balance?.TotalLeaves- balance?.LeavesTaken}, Requested: {daysRequested}" });
                }

                var success = await UpdateLeaveBalanceAsync(leave.EmployeeId, leave.StartDate, leave.EndDate, daysRequested);

                if (!success)
                    return StatusCode(500, "Failed to update leave balance.");

                leave.Status = "Approved";
            }
            else
            {
                leave.Status = request.Status;
            }

            _context.LeaveRequests.Update(leave);
            await _context.SaveChangesAsync();

            return Ok($"Leave status updated to {leave.Status}");
        }

        public class ApproveLeaveRequest
        {
            public string? Status { get; set; }
        }
    }
}
