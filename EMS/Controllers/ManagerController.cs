using EMS; // Or your actual namespace where IEmailService is
using EMS.ViewModels;
using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EMS.Controllers
{
    public class ManagerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<Users> _userManager;

        public ManagerController(AppDbContext context, RoleManager<IdentityRole> roleManager, UserManager<Users> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }
        private static int CalculateBusinessDays(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
                return 0;

            int businessDays = 0;
            DateTime current = startDate;

            while (current <= endDate)
            {
                // Check if current day is not Saturday (6) or Sunday (0)
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
                current = current.AddDays(1);
            }

            return businessDays;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.UserId == userId);

            if (employee == null)
            {
                return NotFound("Employee not found for current user.");
            }

            var viewModel = new EmployeeProfileViewModel
            {
                Employee = employee,
            };

            return View(viewModel);
        }

        // GET: /Manager/ApproveList
        //[Authorize(Roles = "Manager")]
        public async Task<IActionResult> ApproveList()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var manager = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId);

            if (manager == null)
            {
                return NotFound("Manager not found.");
            }

            var teamEmployeeIds = await _context.Employees
                .Where(e => e.ManagerId == manager.EmployeeId)
                .Select(e => e.EmployeeId)
                .ToListAsync();

            var leaves = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Status == "Pending" && teamEmployeeIds.Contains(l.EmployeeId))
                .ToListAsync();

            return View(leaves);
        }

        // GET: /Manager/Approvals/5
        //[Authorize(Roles = "Manager")]
        public async Task<IActionResult> Approvals(int id)
        {
            var leave = await _context.LeaveRequests
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(l => l.LeaveRequestId == id);

            if (leave == null)
                return NotFound();

            return View(leave);
        }

        // POST: /Manager/Approvals/5
        //[Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approvals(int id, string status)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave == null) return NotFound();

            leave.Status = status;

            if (status == "Approved")
            {
                var balance = await _context.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == leave.EmployeeId);
                if (balance != null)
                {
                    int days = CalculateBusinessDays(leave.StartDate, leave.EndDate)+1;

                    if (balance.LeavesTaken + days > balance.TotalLeaves)
                    {
                        TempData["ToastError"] = "Insufficient leave balance.";
                        return RedirectToAction("ApproveList", "Manager");
                    }

                    balance.LeavesTaken += days;
                    await _context.SaveChangesAsync();
                }
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ApproveList));
        }

        [HttpGet]
        //[Authorize(Roles = "Manager")]
        public async Task<IActionResult> Subordinates()
        {
            var userId = _userManager.GetUserId(User); 
            var user = await _userManager.FindByIdAsync(userId);

            var manager = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
            if (manager == null)
            {
                TempData["ToastError"] = "Manager record not found.";
                return RedirectToAction("Index", "Home");
            }

            var subordinates = await _context.Employees
                .Where(e => e.ManagerId == manager.EmployeeId)
                .ToListAsync();

            return View(subordinates); 
        }

    }
}
