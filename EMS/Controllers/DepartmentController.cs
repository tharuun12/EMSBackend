using EMS.Models;
using EMS.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers
{
    [Route("/department")]
    [ApiController]
    public class DepartmentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DepartmentController(AppDbContext context, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        // GET: 
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var departments = await _context.Department
                .Include(d => d.Manager)
                .ToListAsync();
            return Ok(departments);
        }

        // GET: create
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("create")]
        [Authorize(Roles = "Admin, admin")]
        public IActionResult Create()
        {
            var departmentList = _context.Department.ToList();
            return Ok(departmentList);
        }

        // POST: create
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateDepartment([FromBody] Department department)
        {
            if (string.IsNullOrWhiteSpace(department.DepartmentName))
                return BadRequest(new { message = "Department name is required." });

            // --- your original manager‐validation & assignment logic ---
            if (department.ManagerId.HasValue)
            {
                var selectedEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == department.ManagerId.Value);

                if (selectedEmployee == null)
                    return BadRequest(new { message = "Selected manager does not exist." });

                var existingManagerDepartment = await _context.Department
                    .FirstOrDefaultAsync(d => d.ManagerId == department.ManagerId.Value);

                if (existingManagerDepartment != null)
                    return BadRequest(new { message = $"This employee is already managing {existingManagerDepartment.DepartmentName} department." });

                department.ManagerName = selectedEmployee.FullName;

                if (selectedEmployee.Role != "Manager")
                {
                    selectedEmployee.Role = "Manager";
                    selectedEmployee.RoleID = (await _roleManager.FindByNameAsync("Manager"))?.Id;
                }

                selectedEmployee.ManagerId = null;
            }

            _context.Department.Add(department);
            await _context.SaveChangesAsync();

            if (department.ManagerId.HasValue)
            {
                var manager = await _context.Employees.FindAsync(department.ManagerId.Value);
                if (manager != null)
                {
                    manager.DepartmentId = department.DepartmentId;
                    await _context.SaveChangesAsync();
                }
            }

            var departmentLog = new DepartmentLogs
            {
                DepartmentId = department.DepartmentId,
                DepartmentName = department.DepartmentName,
                ManagerId = department.ManagerId,
                Operation = "Created",
                TimeStamp = DateTime.Now
            };
            _context.departmentLogs.Add(departmentLog);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Department created successfully." });
        }

        // GET: edit/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var department = await _context.Department.FindAsync(id);
            if (department == null)
                return NotFound();

            return Ok(department);
        }

        // PUT: api/department/edit/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] Department department)
        {
            if (id != department.DepartmentId)
                return NotFound();

            var existingDepartment = await _context.Department.FindAsync(id);
            if (existingDepartment == null)
                return NotFound();

            // --- your original edit logic, manager removal/assignment, field updates ---
            int? oldManagerId = existingDepartment.ManagerId;
            int? newManagerId = department.ManagerId;

            if (oldManagerId != newManagerId)
            {
                if (newManagerId.HasValue)
                {
                    var newManager = await _context.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeId == newManagerId.Value);

                    if (newManager == null)
                        return BadRequest(new { message = "Selected manager does not exist." });

                    var conflict = await _context.Department
                        .FirstOrDefaultAsync(d => d.ManagerId == newManagerId.Value && d.DepartmentId != id);
                    if (conflict != null)
                        return BadRequest(new { message = $"This employee is already a manager of {conflict.DepartmentName} department." });

                    if (oldManagerId.HasValue)
                        await HandleManagerRemoval(oldManagerId.Value, id);

                    await HandleManagerAssignment(newManager, id);

                    existingDepartment.ManagerId = newManagerId;
                    existingDepartment.ManagerName = newManager.FullName;
                }
                else
                {
                    if (oldManagerId.HasValue)
                        await HandleManagerRemoval(oldManagerId.Value, id);

                    existingDepartment.ManagerId = null;
                    existingDepartment.ManagerName = null;
                }
            }

            existingDepartment.DepartmentName = department.DepartmentName;
            _context.Update(existingDepartment);

            var log = new DepartmentLogs
            {
                DepartmentId = department.DepartmentId,
                DepartmentName = department.DepartmentName,
                ManagerId = department.ManagerId,
                Operation = "Updated",
                TimeStamp = DateTime.Now
            };
            _context.departmentLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Department updated successfully." });
        }

        private async Task HandleManagerRemoval(int managerId, int departmentId)
        {
            var oldManager = await _context.Employees.FindAsync(managerId);
            if (oldManager != null && oldManager.Role == "Manager")
            {
                var otherDepartments = await _context.Department
                    .Where(d => d.ManagerId == managerId && d.DepartmentId != departmentId)
                    .ToListAsync();

                if (!otherDepartments.Any())
                {
                    oldManager.Role = "Employee";
                    oldManager.RoleID = (await _roleManager.FindByNameAsync("Employee"))?.Id;

                    var departmentManager = await _context.Department
                        .Where(d => d.DepartmentId == oldManager.DepartmentId && d.ManagerId != managerId)
                        .Select(d => d.ManagerId)
                        .FirstOrDefaultAsync();

                    oldManager.ManagerId = departmentManager;
                }
            }

            var employeesInDepartment = await _context.Employees
                .Where(e => e.DepartmentId == departmentId && e.ManagerId == managerId)
                .ToListAsync();

            foreach (var emp in employeesInDepartment)
                emp.ManagerId = null;
        }

        private async Task HandleManagerAssignment(Employee newManager, int departmentId)
        {
            if (newManager.Role != "Manager")
            {
                newManager.Role = "Manager";
                newManager.RoleID = (await _roleManager.FindByNameAsync("Manager"))?.Id;
            }

            if (newManager.DepartmentId != departmentId)
                newManager.DepartmentId = departmentId;

            newManager.ManagerId = null;

            var employeesInDepartment = await _context.Employees
                .Where(e => e.DepartmentId == departmentId && e.EmployeeId != newManager.EmployeeId)
                .ToListAsync();

            foreach (var emp in employeesInDepartment)
                emp.ManagerId = newManager.EmployeeId;
        }

        // GET: api/department/delete/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _context.Department
                .Include(d => d.Manager)
                .FirstOrDefaultAsync(m => m.DepartmentId == id);

            if (department == null)
                return NotFound();

            return Ok(department);
        }

        // DELETE: /department/delete/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var department = await _context.Department.FindAsync(id);
            if (department == null)
                return NotFound();

            bool hasEmployees = await _context.Employees.AnyAsync(e => e.DepartmentId == id);
            if (hasEmployees)
                return BadRequest(new { message = "Cannot delete department while employees are still assigned." });

            var departmentLog = new DepartmentLogs
            {
                DepartmentId = department.DepartmentId,
                DepartmentName = department.DepartmentName,
                ManagerId = department.ManagerId,
                Operation = "Deleted",
                TimeStamp = DateTime.Now
            };
            _context.departmentLogs.Add(departmentLog);

            _context.Department.Remove(department);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Department deleted successfully." });
        }
    }
}
