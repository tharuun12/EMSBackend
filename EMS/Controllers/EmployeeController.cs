using EMS.Models;
using EMS.ViewModels;
using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace EMS.Web.Controllers
{
    [Route("/employees")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<Users> _userManager;

        public EmployeeController(
            AppDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<Users> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // GET: /employees
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet]
        public async Task<IActionResult> EmployeeList()
        {
            var employees = await _context.Employees
                .Include(e => e.Department)
                .ToListAsync();
            return Ok(employees);
        }

        // GET: /employees/create
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var departments = await _context.Department.ToListAsync();
            var managers = await _context.Employees.ToListAsync();
            var roles = (await _roleManager.Roles.ToListAsync()).Select(r => r.Name);
            return Ok(new { Departments = departments, Managers = managers, Roles = roles });
        }

        // POST: /employees/create
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] EmployeeViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var normalizedEmail = model.Email.Trim().ToLower();
            var existingEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email.ToLower().Trim() == normalizedEmail);
            if (existingEmployee != null)
                return BadRequest(new { message = "A user with this Email already exists." });

            var department = await _context.Department.FindAsync(model.DepartmentId);
            if (department == null)
                return BadRequest(new { message = "Selected department does not exist." });

            if (model.Role == "Manager")
            {
                if (department.ManagerId != null)
                    return BadRequest(new { message = "This department already has a manager assigned. Please change the current manager to employee role first." });
                if (!await _context.Employees.AnyAsync(e => e.Role == "Admin"))
                    return BadRequest(new { message = "Please create an Admin before adding a Manager." });
            }
            else if (model.Role == "Employee")
            {
                if (department.ManagerId == null)
                    return BadRequest(new { message = "Please assign a Manager to the Department first." });
            }

            var role = await _roleManager.FindByNameAsync(model.Role);
            if (role == null)
                return BadRequest(new { message = "Selected role is invalid." });

            var employee = new Employee
            {
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Role = model.Role,
                RoleID = role.Id,
                IsActive = model.IsActive,
                DepartmentId = model.DepartmentId,
                ManagerId = model.Role == "Manager" ? null : department.ManagerId,
                LeaveBalance = model.LeaveBalance
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            if (model.Role == "Manager")
            {
                department.ManagerId = employee.EmployeeId;
                department.ManagerName = employee.FullName;

                var subs = await _context.Employees
                    .Where(e => e.DepartmentId == model.DepartmentId && e.EmployeeId != employee.EmployeeId)
                    .ToListAsync();
                subs.ForEach(e => e.ManagerId = employee.EmployeeId);
                await _context.SaveChangesAsync();
            }

            var employeeLog = new EmployeeLog
            {
                EmployeeId = employee.EmployeeId,
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Role = model.Role,
                RoleID = employee.RoleID,
                IsActive = model.IsActive,
                DepartmentId = model.DepartmentId,
                ManagerId = employee.ManagerId,
                Operation = "Created",
                TimeStamp = DateTime.Now
            };
            var leaveBalance = new LeaveBalance
            {
                EmployeeId = employee.EmployeeId,
                TotalLeaves = model.LeaveBalance
            };

            _context.LeaveBalances.Add(leaveBalance);
            _context.EmployeeLog.Add(employeeLog);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Employee created successfully!" });
        }

        // GET: /employees/edit/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();
            var departments = await _context.Department.ToListAsync();
            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return Ok(new { Employee = employee, Departments = departments, Roles = roles });
        }

        // PUT: /employees/edit/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] Employee employee)
        {
            if (id != employee.EmployeeId) return NotFound();

            var existing = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.EmployeeId == id);
            if (existing == null) return NotFound();

            bool wasManager = existing.Role == "Manager";
            bool willBeManager = employee.Role == "Manager";
            int oldDeptId = existing.DepartmentId;
            int newDeptId = employee.DepartmentId;

            var newDept = await _context.Department.FindAsync(newDeptId);
            if (newDept == null)
                return BadRequest(new { message = "Selected department does not exist." });

            // Role‐change validations
            if (willBeManager && !wasManager)
            {
                if (newDept.ManagerId != null && newDept.ManagerId != id)
                    return BadRequest(new { message = "This department already has a manager assigned. Please change the current manager to employee role first." });
            }
            else if (willBeManager && wasManager && newDeptId != oldDeptId)
            {
                if (newDept.ManagerId != null && newDept.ManagerId != id)
                    return BadRequest(new { message = "The target department already has a manager assigned." });
            }
            else if (!willBeManager && employee.Role == "Employee")
            {
                if (newDept.ManagerId == null)
                    return BadRequest(new { message = "Cannot assign employee to a department without a manager. Please assign a manager to the department first." });
            }

            var roleObj = await _roleManager.FindByNameAsync(employee.Role);
            if (roleObj == null)
                return BadRequest(new { message = "Selected role is invalid." });

            // Update ASP.NET Identity roles
            if (!string.IsNullOrEmpty(existing.UserId))
            {
                var user = await _userManager.FindByIdAsync(existing.UserId);
                if (user != null)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, employee.Role);
                }
            }

            // Leave balance adjustment
            if (employee.LeaveBalance != existing.LeaveBalance)
            {
                var lb = await _context.LeaveBalances.FirstOrDefaultAsync(l => l.EmployeeId == id);
                if (lb != null)
                {
                    lb.TotalLeaves = employee.LeaveBalance + lb.TotalLeaves;
                    _context.LeaveBalances.Update(lb);
                    await _context.SaveChangesAsync();
                }
            }

            // Update fields
            existing.FullName = employee.FullName;
            existing.Email = employee.Email;
            existing.PhoneNumber = employee.PhoneNumber;
            existing.Role = employee.Role;
            existing.RoleID = roleObj.Id;
            existing.IsActive = employee.IsActive;
            existing.DepartmentId = newDeptId;
            existing.LeaveBalance = employee.LeaveBalance;
            existing.ManagerId = willBeManager ? null : newDept.ManagerId;

            // Handle manager demotion/promotion/transfer
            if (wasManager && !willBeManager)
                await HandleManagerDemotion(id);
            else if (!wasManager && willBeManager)
                await HandleManagerPromotion(id, existing.FullName, newDeptId);
            else if (wasManager && willBeManager && oldDeptId != newDeptId)
                await HandleManagerDepartmentChange(id, existing.FullName, oldDeptId, newDeptId);

            // Log
            var log = new EmployeeLog
            {
                EmployeeId = existing.EmployeeId,
                FullName = existing.FullName,
                Email = existing.Email,
                PhoneNumber = existing.PhoneNumber,
                Role = existing.Role,
                RoleID = existing.RoleID,
                IsActive = existing.IsActive,
                DepartmentId = existing.DepartmentId,
                ManagerId = existing.ManagerId,
                Operation = "Updated",
                TimeStamp = DateTime.Now
            };
            _context.EmployeeLog.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Employee updated successfully!" });
        }

        private async Task HandleManagerDemotion(int empId)
        {
            var depts = await _context.Department.Where(d => d.ManagerId == empId).ToListAsync();
            foreach (var d in depts)
            {
                d.ManagerId = null;
                d.ManagerName = null;
                _context.departmentLogs.Add(new DepartmentLogs
                {
                    DepartmentId = d.DepartmentId,
                    DepartmentName = d.DepartmentName,
                    ManagerId = null,
                    Operation = "Manager Demoted",
                    TimeStamp = DateTime.Now
                });
            }
            var subs = await _context.Employees.Where(e => e.ManagerId == empId).ToListAsync();
            subs.ForEach(e => e.ManagerId = null);
        }

        private async Task HandleManagerPromotion(int empId, string name, int deptId)
        {
            var d = await _context.Department.FindAsync(deptId);
            if (d != null)
            {
                d.ManagerId = empId;
                d.ManagerName = name;
                var subs = await _context.Employees
                    .Where(e => e.DepartmentId == deptId && e.EmployeeId != empId)
                    .ToListAsync();
                subs.ForEach(e => e.ManagerId = empId);
                _context.departmentLogs.Add(new DepartmentLogs
                {
                    DepartmentId = d.DepartmentId,
                    DepartmentName = d.DepartmentName,
                    ManagerId = empId,
                    Operation = "Manager Assigned",
                    TimeStamp = DateTime.Now
                });
            }
        }

        private async Task HandleManagerDepartmentChange(int empId, string name, int oldDept, int newDept)
        {
            var old = await _context.Department.FindAsync(oldDept);
            if (old != null && old.ManagerId == empId)
            {
                old.ManagerId = null;
                old.ManagerName = null;
                var subs1 = await _context.Employees
                    .Where(e => e.DepartmentId == oldDept && e.ManagerId == empId)
                    .ToListAsync();
                subs1.ForEach(e => e.ManagerId = null);
                _context.departmentLogs.Add(new DepartmentLogs
                {
                    DepartmentId = old.DepartmentId,
                    DepartmentName = old.DepartmentName,
                    ManagerId = null,
                    Operation = "Manager Transferred Out",
                    TimeStamp = DateTime.Now
                });
            }
            await HandleManagerPromotion(empId, name, newDept);
        }

        // GET: api/employee/delete/5
        [Authorize(Roles = "Admin")]
        [HttpGet("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.EmployeeId == id);
            if (employee == null) return NotFound();
            return Ok(employee);
        }

        // DELETE: api/employee/delete/5
        [Authorize(Roles = "Admin")]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound();

            if (await _context.Department.AnyAsync(d => d.ManagerId == employee.EmployeeId))
                return BadRequest(new { message = "Cannot delete employee: assigned as department manager." });

            var user = await _userManager.FindByEmailAsync(employee.Email);
            if (user != null)
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                await _userManager.UpdateAsync(user);
            }

            var subs = await _context.Employees.Where(e => e.ManagerId == employee.EmployeeId).ToListAsync();
            subs.ForEach(e => e.ManagerId = null);

            var depts = await _context.Department.Where(d => d.ManagerId == employee.EmployeeId).ToListAsync();
            foreach (var d in depts)
            {
                d.ManagerId = null;
                d.ManagerName = null;
                _context.departmentLogs.Add(new DepartmentLogs
                {
                    DepartmentId = d.DepartmentId,
                    DepartmentName = d.DepartmentName,
                    ManagerId = null,
                    Operation = "Unassigned Manager (Deleted)",
                    TimeStamp = DateTime.Now
                });
            }

            _context.EmployeeLog.Add(new EmployeeLog
            {
                EmployeeId = employee.EmployeeId,
                FullName = employee.FullName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                Role = employee.Role,
                RoleID = employee.RoleID,
                IsActive = employee.IsActive,
                DepartmentId = employee.DepartmentId,
                ManagerId = employee.ManagerId,
                Operation = "Deleted",
                TimeStamp = DateTime.Now
            });

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Employee deleted and changes updated successfully." });
        }

        // GET: api/employee/filter?departmentId=1&role=Manager
        [Authorize(Roles = "Admin")]
        [HttpGet("filter")]
        public async Task<IActionResult> Filter(int? departmentId, string? role)
        {
            var q = _context.Employees.Include(e => e.Department).AsQueryable();
            if (departmentId.HasValue) q = q.Where(e => e.DepartmentId == departmentId.Value);
            if (!string.IsNullOrEmpty(role)) q = q.Where(e => e.Role == role);
            var list = await q.ToListAsync();
            return Ok(list);
        }

        // GET: api/employee/my-leaves
        [Authorize]
        [HttpGet("my-leaves")]
        public async Task<IActionResult> MyLeaves(string employeeId)
        {
            //int userId = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            var empId = 0;
            if (!int.TryParse(employeeId, out empId))
                return BadRequest(new { message = "Invalid employee ID format." });
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == empId);
            if (emp == null) return NotFound(new { message = "Employee record not found." });

            var now = DateTime.Now;
            var leaves = await _context.LeaveRequests
                .Where(l => l.EmployeeId == emp.EmployeeId
                         && l.StartDate.Month == now.Month
                         && l.StartDate.Year == now.Year)
                .ToListAsync();
            return Ok(leaves);
        }

        // GET: /employees/managers
        [Authorize]
        [HttpGet("managers")]
        public async Task<IActionResult> ManagersList()
        {
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager")
                .Select(e => new ManagerDetailsViewModel
                {
                    EmployeeId = e.EmployeeId,
                    FullName = e.FullName,
                    Email = e.Email,
                    DepartmentId = _context.Department.Where(d => d.ManagerId == e.EmployeeId)
                                                        .Select(d => d.DepartmentId)
                                                        .FirstOrDefault(),
                    DepartmentName = _context.Department.Where(d => d.ManagerId == e.EmployeeId)
                                                        .Select(d => d.DepartmentName)
                                                        .FirstOrDefault()
                })
                .ToListAsync();
            return Ok(managers);
        }

        // GET: /employees/current-month-info
        [Authorize]
        [HttpGet("current-month-info")]
        public async Task<IActionResult> CurrentMonthInfo(int id)
        {
            var user = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == id);
            if (string.IsNullOrEmpty(user?.UserId)) return Unauthorized();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.UserId == user.UserId);
            if (employee == null) return NotFound(new { message = "Employee not found for current user." });

            var now = DateTime.Now;
            var monthly = await _context.LeaveRequests
                .Where(l => l.EmployeeId == employee.EmployeeId
                         && l.StartDate.Month == now.Month
                         && l.StartDate.Year == now.Year)
                .ToListAsync();

            var daysOnLeave = monthly.Where(l => l.Status == "Approved")
                                     .Sum(l => (l.EndDate - l.StartDate).Days + 1);

            string managerName = "N/A";
            if (employee.ManagerId.HasValue)
            {
                var m = await _context.Employees.FindAsync(employee.ManagerId);
                managerName = m?.FullName ?? "N/A";
            }

            var vm = new CurrentMonthEmployeeViewModel
            {
                Employee = employee,
                ManagerName = managerName,
                CurrentMonth = now.ToString("MMMM yyyy"),
                LeaveRequests = monthly,
                DaysOnLeave = daysOnLeave,
                RemainingLeaveBalance = employee.LeaveBalance - daysOnLeave
            };
            return Ok(vm);
        }

        // GET: /employees/profile
        [Authorize]

        [HttpGet("profile")]
        public async Task<IActionResult> Profile(int id)
        {
            var user = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == id);
            var userId = user?.UserId;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.UserId == userId);
            if (employee == null) return NotFound(new { message = "Employee not found for current user." });

            string managerName = "N/A";
            if (employee.ManagerId.HasValue)
            {
                var m = await _context.Employees.FindAsync(employee.ManagerId);
                managerName = m?.FullName ?? "N/A";
            }

            var vm = new EmployeeProfileViewModel
            {
                Employee = employee,
                ManagerName = managerName
            };
            return Ok(vm);
        }
    }
}
