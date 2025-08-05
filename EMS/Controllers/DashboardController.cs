using EMS.ViewModels;
using EMS.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var totalEmployees = await _context.Employees.CountAsync();
            var activeEmployees = await _context.Employees.CountAsync(e => e.IsActive == true);
            var totalDepartments = await _context.Department.CountAsync();

            // Leave Request Analytics 
            var totalLeaveRequests = await _context.LeaveRequests.CountAsync();
            var approvedLeaves = await _context.LeaveRequests.CountAsync(l => l.Status == "Approved");
            var pendingLeaves = await _context.LeaveRequests.CountAsync(l => l.Status == "Pending");
            var rejectedLeaves = await _context.LeaveRequests.CountAsync(l => l.Status == "Rejected");

            var recentEmployees = await _context.Employees
                .Take(5)
                .ToListAsync();

            var departmentStats = await _context.Department
                .Include(d => d.Employees)
                .Select(d => new DepartmentStatsViewModel
                {
                    Name = d.DepartmentName,
                    EmployeeCount = d.Employees!.Count()
                }).ToListAsync();

            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.ActiveEmployees = activeEmployees;
            ViewBag.TotalDepartments = totalDepartments;
            ViewBag.RecentEmployees = recentEmployees;
            ViewBag.DepartmentStats = departmentStats;

            // Add leave data to ViewBag
            ViewBag.TotalLeaveRequests = totalLeaveRequests;
            ViewBag.ApprovedLeaves = approvedLeaves;
            ViewBag.PendingLeaves = pendingLeaves;
            ViewBag.RejectedLeaves = rejectedLeaves;

            var result = new
            {
                totalEmployees,
                activeEmployees,
                totalDepartments,
                recentEmployees,
                departmentStats,
                totalLeaveRequests,
                approvedLeaves,
                pendingLeaves,
                rejectedLeaves
            };

            return Json(result);
            //return View(departmentStats);
        }
    }
}
