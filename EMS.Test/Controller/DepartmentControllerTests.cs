using EMS.Models;
using EMS.Web.Controllers;
using EMS.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace EMS.Tests.Controllers
{
    public class DepartmentControllerTests
    {
        private async Task<AppDbContext> GetInMemoryDbContextAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new AppDbContext(options);

            // Seed sample manager
            var manager = new Employee
            {
                EmployeeId = 1,
                FullName = "Alice Johnson",
                Email = "alice@example.com",
                PhoneNumber = "1234567890",
                Role = "Manager",
            };
            await context.Employees.AddAsync(manager);

            // Seed department
            var dept = new Department
            {
                DepartmentId = 1,
                DepartmentName = "IT",
                ManagerId = 1,
                ManagerName = "Alice Johnson"
            };
            await context.Department.AddAsync(dept);

            await context.SaveChangesAsync();
            return context;
        }

        private RoleManager<IdentityRole> GetMockRoleManager()
        {
            var store = new Mock<IRoleStore<IdentityRole>>();
            var roleManager = new Mock<RoleManager<IdentityRole>>(
                store.Object,
                null, null, null, null);

            // Setup FindByNameAsync for roles
            roleManager.Setup(r => r.FindByNameAsync("Manager"))
                .ReturnsAsync(new IdentityRole
                {
                    Id = "manager-role-id",
                    Name = "Manager"
                });

            roleManager.Setup(r => r.FindByNameAsync("Employee"))
                .ReturnsAsync(new IdentityRole
                {
                    Id = "employee-role-id",
                    Name = "Employee"
                });

            return roleManager.Object;
        }

        private DepartmentController SetupControllerWithContext(AppDbContext context, string role = "Admin")
        {
            var roleManager = GetMockRoleManager();
            var controller = new DepartmentController(context, roleManager);

            // Setup authentication and authorization
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.NameIdentifier, "testuser-id"),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            return controller;
        }

        [Fact]
        public async Task Index_ReturnsDepartments()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = await controller.Index();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var departments = Assert.IsAssignableFrom<IEnumerable<Department>>(okResult.Value);
            Assert.Single(departments);
        }

        [Fact]
        public async Task Create_ReturnsOkWithDepartmentList()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = controller.Create();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var departments = Assert.IsAssignableFrom<List<Department>>(okResult.Value);
            Assert.Single(departments);
        }

        [Fact]
        public async Task CreateDepartment_EmptyDepartmentName_ReturnsBadRequest()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            var newDepartment = new Department
            {
                DepartmentName = "",  // Empty name
                ManagerId = 1
            };

            // Act
            var result = await controller.CreateDepartment(newDepartment);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(badRequestResult.Value),
                new { message = "" }
            );
            Assert.Equal("Department name is required.", responseObj.message);
        }

        [Fact]
        public async Task CreateDepartment_InvalidManager_ReturnsBadRequest()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            var newDepartment = new Department
            {
                DepartmentName = "HR",
                ManagerId = 99 // Non-existent manager
            };

            // Act
            var result = await controller.CreateDepartment(newDepartment);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(badRequestResult.Value),
                new { message = "" }
            );
            Assert.Equal("Selected manager does not exist.", responseObj.message);
        }

        [Fact]
        public async Task CreateDepartment_ManagerAlreadyAssigned_ReturnsBadRequest()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();

            // Add another department with same manager
            var dept2 = new Department
            {
                DepartmentId = 2,
                DepartmentName = "HR",
                ManagerId = 1,
                ManagerName = "Alice Johnson"
            };
            await context.Department.AddAsync(dept2);
            await context.SaveChangesAsync();

            var controller = SetupControllerWithContext(context);

            var newDepartment = new Department
            {
                DepartmentName = "Finance",
                ManagerId = 1  // Already managing IT and HR
            };

            // Act
            var result = await controller.CreateDepartment(newDepartment);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(badRequestResult.Value),
                new { message = "" }
            );
            Assert.Contains("This employee is already managing", responseObj.message);
        }

        [Fact]
        public async Task Edit_Get_ReturnsDepartment()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = await controller.Edit(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var department = Assert.IsType<Department>(okResult.Value);
            Assert.Equal(1, department.DepartmentId);
            Assert.Equal("IT", department.DepartmentName);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = await controller.Edit(999); // Non-existent ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Put_ValidUpdate_ReturnsSuccess()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            var updatedDepartment = new Department
            {
                DepartmentId = 1,
                DepartmentName = "Updated IT",
                ManagerId = 1
            };

            // Act
            var result = await controller.Edit(1, updatedDepartment);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(okResult.Value),
                new { message = "" }
            );
            Assert.Equal("Department updated successfully.", responseObj.message);

            var updated = await context.Department.FindAsync(1);
            Assert.Equal("Updated IT", updated.DepartmentName);

            // Verify department log was created
            var log = await context.departmentLogs.FirstOrDefaultAsync(l => l.DepartmentId == 1 && l.Operation == "Updated");
            Assert.NotNull(log);
        }

        [Fact]
        public async Task Edit_Put_MismatchedIds_ReturnsNotFound()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            var updatedDepartment = new Department
            {
                DepartmentId = 2, // Different from route ID
                DepartmentName = "Updated IT",
                ManagerId = 1
            };

            // Act
            var result = await controller.Edit(1, updatedDepartment);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Put_DepartmentNotFound_ReturnsNotFound()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            var updatedDepartment = new Department
            {
                DepartmentId = 999, // Non-existent ID
                DepartmentName = "Updated IT",
                ManagerId = 1
            };

            // Act
            var result = await controller.Edit(999, updatedDepartment);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Put_NewManagerDoesNotExist_ReturnsBadRequest()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            var updatedDepartment = new Department
            {
                DepartmentId = 1,
                DepartmentName = "Updated IT",
                ManagerId = 999 // Non-existent manager
            };

            // Act
            var result = await controller.Edit(1, updatedDepartment);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(badRequestResult.Value),
                new { message = "" }
            );
            Assert.Equal("Selected manager does not exist.", responseObj.message);
        }

        [Fact]
        public async Task Edit_Put_ManagerAlreadyAssignedToAnotherDepartment_ReturnsBadRequest()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();

            // Add another manager
            var manager2 = new Employee
            {
                EmployeeId = 2,
                FullName = "Bob Smith",
                Email = "bob@example.com",
                PhoneNumber = "9876543210",
                Role = "Manager",
            };
            await context.Employees.AddAsync(manager2);

            // Add another department with manager2
            var dept2 = new Department
            {
                DepartmentId = 2,
                DepartmentName = "HR",
                ManagerId = 2,
                ManagerName = "Bob Smith"
            };
            await context.Department.AddAsync(dept2);
            await context.SaveChangesAsync();

            var controller = SetupControllerWithContext(context);

            var updatedDepartment = new Department
            {
                DepartmentId = 1,
                DepartmentName = "Updated IT",
                ManagerId = 2  // Already managing HR department
            };

            // Act
            var result = await controller.Edit(1, updatedDepartment);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(badRequestResult.Value),
                new { message = "" }
            );
            Assert.Contains("This employee is already a manager of", responseObj.message);
        }

        [Fact]
        public async Task Delete_Get_ReturnsDepartment()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = await controller.Delete(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var department = Assert.IsType<Department>(okResult.Value);
            Assert.Equal(1, department.DepartmentId);
            Assert.Equal("IT", department.DepartmentName);
        }

        [Fact]
        public async Task Delete_Get_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = await controller.Delete(999); // Non-existent ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteConfirmed_ValidDeletion_ReturnsSuccess()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = await controller.DeleteConfirmed(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(okResult.Value),
                new { message = "" }
            );
            Assert.Equal("Department deleted successfully.", responseObj.message);

            var deletedDepartment = await context.Department.FindAsync(1);
            Assert.Null(deletedDepartment);

            // Verify department log was created
            var log = await context.departmentLogs.FirstOrDefaultAsync(l => l.Operation == "Deleted");
            Assert.NotNull(log);
        }

        [Fact]
        public async Task DeleteConfirmed_DepartmentNotFound_ReturnsNotFound()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            // Act
            var result = await controller.DeleteConfirmed(999); // Non-existent ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteConfirmed_WithEmployees_ReturnsBadRequest()
        {
            // Arrange
            var context = await GetInMemoryDbContextAsync();
            var controller = SetupControllerWithContext(context);

            context.Employees.Add(new Employee
            {
                EmployeeId = 99,
                FullName = "Test Employee",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Developer",
                DepartmentId = 1
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.DeleteConfirmed(1);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var responseObj = JsonConvert.DeserializeAnonymousType(
                JsonConvert.SerializeObject(badRequestResult.Value),
                new { message = "" }
            );
            Assert.Equal("Cannot delete department while employees are still assigned.", responseObj.message);

            // Verify department still exists
            var department = await context.Department.FindAsync(1);
            Assert.NotNull(department);
        }
    }
}