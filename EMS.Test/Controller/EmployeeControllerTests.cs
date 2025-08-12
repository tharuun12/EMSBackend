using EMS.Models;
using EMS.ViewModels;
using EMS.Web.Controllers;
using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace EMS.Tests.Controller
{
    public class EmployeeControllerTests
    {
        private AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        private EmployeeController GetController(
            AppDbContext context,
            Mock<RoleManager<IdentityRole>> roleManagerMock,
            Mock<UserManager<Users>> userManagerMock,
            ClaimsPrincipal? user = null)
        {
            var controller = new EmployeeController(context, roleManagerMock.Object, userManagerMock.Object);
            var httpContext = new DefaultHttpContext();
            if (user != null)
                httpContext.User = user;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private Mock<RoleManager<IdentityRole>> MockRoleManager()
        {
            var store = new Mock<IRoleStore<IdentityRole>>();
            var roleManager = new Mock<RoleManager<IdentityRole>>(
                store.Object,
                Array.Empty<IRoleValidator<IdentityRole>>(),
                null!,
                null!,
                null!);

            var roles = new List<IdentityRole>
                {
                    new IdentityRole("Admin"),
                    new IdentityRole("Manager"),
                    new IdentityRole("Employee")
                };

            // Set up the Roles property to return our queryable collection
            roleManager.Setup(r => r.Roles).Returns(roles.AsQueryable());

            // For tests that need to find roles by name
            roleManager.Setup(r => r.FindByNameAsync(It.IsAny<string>()))
                .Returns<string>(name => Task.FromResult(
                    roles.FirstOrDefault(r => r.Name == name)));

            return roleManager;
        }

        private Mock<UserManager<Users>> MockUserManager()
        {
            var store = new Mock<IUserStore<Users>>();
            var userManager = new Mock<UserManager<Users>>(
                store.Object,
                null!,
                null!,
                Array.Empty<IUserValidator<Users>>(),
                Array.Empty<IPasswordValidator<Users>>(),
                null!,
                new IdentityErrorDescriber(),
                null!,
                null!  // ILogger<UserManager<Users>>
            );

            // Set up default behavior
            userManager.Setup(m => m.UpdateAsync(It.IsAny<Users>()))
                .ReturnsAsync(IdentityResult.Success);

            return userManager;
        }

        private ClaimsPrincipal GetUserWithRole(string userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "mock");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public async Task EmployeeList_ReturnsOkResultWithEmployees()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT" });
            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "Test Employee",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            });
            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            // Act
            var result = await controller.EmployeeList();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var employees = Assert.IsAssignableFrom<List<Employee>>(okResult.Value);
            Assert.Single(employees);
            Assert.Equal("Test Employee", employees[0].FullName);
        }
   
        [Fact]
        public async Task Create_Post_ValidEmployeeModel_AddsEmployeeAndReturnsOk()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT", ManagerId = 2 });
            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567891",
                Role = "Manager",
                DepartmentId = 1,
                IsActive = true
            });
            context.SaveChanges();

            var roleManagerMock = MockRoleManager();
            roleManagerMock.Setup(r => r.FindByNameAsync("Employee")).ReturnsAsync(new IdentityRole { Id = "role1", Name = "Employee" });

            var controller = GetController(context, roleManagerMock, MockUserManager(), GetUserWithRole("1", "Admin"));
            var model = new EmployeeViewModel
            {
                FullName = "Test User",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                IsActive = true,
                DepartmentId = 1,
                LeaveBalance = 20
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Employee created successfully", okResult.Value.ToString());

            // Verify employee was created with correct details
            var emp = await context.Employees.FirstOrDefaultAsync(e => e.Email == "test@example.com");
            Assert.NotNull(emp);
            Assert.Equal("Test User", emp.FullName);
            Assert.Equal(2, emp.ManagerId);
            Assert.Equal("role1", emp.RoleID);
            Assert.Equal(20, emp.LeaveBalance);

            // Verify employee log was created
            var log = await context.EmployeeLog.FirstOrDefaultAsync(l => l.Email == "test@example.com");
            Assert.NotNull(log);
            Assert.Equal("Created", log.Operation);
        }

        [Fact]
        public async Task Create_Post_ValidManagerModel_AddsManagerAndUpdatesDepartment()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT" }); // No manager
            context.Employees.Add(new Employee
            {
                EmployeeId = 10,
                FullName = "Admin",
                Email = "admin@example.com",
                PhoneNumber = "1234567899",
                Role = "Admin",
                DepartmentId = 1,
                IsActive = true
            });
            context.SaveChanges();

            var roleManagerMock = MockRoleManager();
            roleManagerMock.Setup(r => r.FindByNameAsync("Manager")).ReturnsAsync(new IdentityRole { Id = "role2", Name = "Manager" });

            var controller = GetController(context, roleManagerMock, MockUserManager(), GetUserWithRole("10", "Admin"));
            var model = new EmployeeViewModel
            {
                FullName = "New Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567892",
                Role = "Manager",
                IsActive = true,
                DepartmentId = 1,
                LeaveBalance = 20
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Employee created successfully", okResult.Value.ToString());

            // Verify employee was created with correct details
            var emp = await context.Employees.FirstOrDefaultAsync(e => e.Email == "manager@example.com");
            Assert.NotNull(emp);
            Assert.Equal("New Manager", emp.FullName);
            Assert.Equal("role2", emp.RoleID);

            // Verify department was updated
            var dept = await context.Department.FirstOrDefaultAsync(d => d.DepartmentId == 1);
            Assert.Equal(emp.EmployeeId, dept.ManagerId);
            Assert.Equal("New Manager", dept.ManagerName);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            // Act
            var result = await controller.Edit(99); // Non-existent ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Put_ValidUpdate_UpdatesEmployeeAndReturnsOk()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "Old Name",
                Email = "old@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            });
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT", ManagerId = 2 });
            context.SaveChanges();

            var roleManagerMock = MockRoleManager();
            roleManagerMock.Setup(r => r.FindByNameAsync("Employee")).ReturnsAsync(new IdentityRole { Id = "role1", Name = "Employee" });

            var controller = GetController(context, roleManagerMock, MockUserManager(), GetUserWithRole("1", "Admin"));

            var updatedEmployee = new Employee
            {
                EmployeeId = 1,
                FullName = "New Name",
                Email = "new@example.com",
                PhoneNumber = "0987654321",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = false
            };

            // Act
            var result = await controller.Edit(1, updatedEmployee);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Employee updated successfully", okResult.Value.ToString());

            var emp = await context.Employees.FirstAsync(e => e.EmployeeId == 1);
            Assert.Equal("New Name", emp.FullName);
            Assert.Equal("new@example.com", emp.Email);
            Assert.Equal("0987654321", emp.PhoneNumber);
            Assert.False(emp.IsActive);
            Assert.Equal("role1", emp.RoleID);
        }

        [Fact]
        public async Task Edit_Put_MismatchedIds_ReturnsNotFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));
            var updatedEmployee = new Employee
            {
                EmployeeId = 99, // Different from route ID
                FullName = "Test",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            };

            // Act
            var result = await controller.Edit(1, updatedEmployee);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Get_ValidId_ReturnsOkWithEmployee()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT" });
            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "Test User",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            });
            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            // Act
            var result = await controller.Delete(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var employee = Assert.IsType<Employee>(okResult.Value);
            Assert.Equal(1, employee.EmployeeId);
            Assert.Equal("Test User", employee.FullName);
        }

        [Fact]
        public async Task Delete_Get_InvalidId_ReturnsNotFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            // Act
            var result = await controller.Delete(99); // Non-existent ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteConfirmed_EmployeeWithNoManagerRole_DeletesAndReturnsOk()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "Test Employee",
                Email = "employee@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            });
            context.SaveChanges();

            var userManagerMock = MockUserManager();
            userManagerMock.Setup(u => u.FindByEmailAsync("employee@example.com"))
                .ReturnsAsync(new Users { Email = "employee@example.com" });

            var controller = GetController(context, MockRoleManager(), userManagerMock, GetUserWithRole("1", "Admin"));

            // Act
            var result = await controller.DeleteConfirmed(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Employee deleted and changes updated successfully", okResult.Value.ToString());

            // Employee should be deleted
            Assert.Empty(await context.Employees.ToListAsync());

            // Log should be created
            var log = await context.EmployeeLog.FirstOrDefaultAsync(l => l.Email == "employee@example.com");
            Assert.NotNull(log);
            Assert.Equal("Deleted", log.Operation);

            // User should be locked out
            userManagerMock.Verify(u => u.UpdateAsync(It.Is<Users>(
                user => user.Email == "employee@example.com" && user.LockoutEnabled == true
            )), Times.Once);
        }

        [Fact]
        public async Task DeleteConfirmed_EmployeeAssignedAsManager_ReturnsBadRequest()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567890",
                Role = "Manager",
                DepartmentId = 1,
                IsActive = true
            });
            context.Department.Add(new Department
            {
                DepartmentId = 1,
                DepartmentName = "IT",
                ManagerId = 1,
                ManagerName = "Manager"
            });
            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            // Act
            var result = await controller.DeleteConfirmed(1);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Cannot delete employee: assigned as department manager", badRequestResult.Value.ToString());

            // Employee should not be deleted
            Assert.Single(await context.Employees.ToListAsync());

            // Department should still have manager
            var dept = await context.Department.FirstAsync(d => d.DepartmentId == 1);
            Assert.Equal(1, dept.ManagerId);
            Assert.Equal("Manager", dept.ManagerName);
        }

        [Fact]
        public async Task Filter_WithDepartmentFilter_ReturnsMatchingEmployees()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT" });
            context.Department.Add(new Department { DepartmentId = 2, DepartmentName = "HR" });

            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "IT Employee",
                Email = "it@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            });

            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "HR Employee",
                Email = "hr@example.com",
                PhoneNumber = "1234567891",
                Role = "Employee",
                DepartmentId = 2,
                IsActive = true
            });

            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            // Act
            var result = await controller.Filter(departmentId: 1, role: null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var employees = Assert.IsAssignableFrom<List<Employee>>(okResult.Value);
            Assert.Single(employees);
            Assert.Equal("IT Employee", employees[0].FullName);
        }

        [Fact]
        public async Task MyLeaves_ValidEmployeeId_ReturnsLeaveRequests()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "Test Employee",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            };
            context.Employees.Add(employee);

            var now = DateTime.Now;
            var leaveRequest = new LeaveRequest
            {
                EmployeeId = 1,
                StartDate = now,
                EndDate = now.AddDays(3),
                Reason = "Vacation",
                Status = "Pending",
                RequestDate = now.AddDays(-1)
            };
            context.LeaveRequests.Add(leaveRequest);
            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Employee"));

            // Act
            var result = await controller.MyLeaves("1");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var leaves = Assert.IsAssignableFrom<List<LeaveRequest>>(okResult.Value);
            Assert.Single(leaves);
            Assert.Equal("Pending", leaves[0].Status);
        }

        [Fact]
        public async Task ManagersList_ReturnsOnlyManagers()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT", ManagerId = 1 });
            context.Department.Add(new Department { DepartmentId = 2, DepartmentName = "HR", ManagerId = 2 });

            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "IT Manager",
                Email = "itmanager@example.com",
                PhoneNumber = "1234567890",
                Role = "Manager",
                DepartmentId = 1,
                IsActive = true
            });

            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "HR Manager",
                Email = "hrmanager@example.com",
                PhoneNumber = "1234567891",
                Role = "Manager",
                DepartmentId = 2,
                IsActive = true
            });

            context.Employees.Add(new Employee
            {
                EmployeeId = 3,
                FullName = "Employee",
                Email = "employee@example.com",
                PhoneNumber = "1234567892",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            });

            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("admin", "Admin"));

            // Act
            var result = await controller.ManagersList();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var managers = Assert.IsAssignableFrom<List<ManagerDetailsViewModel>>(okResult.Value);
            Assert.Equal(2, managers.Count);

            var itManager = managers.First(m => m.FullName == "IT Manager");
            Assert.Equal(1, itManager.DepartmentId);
            Assert.Equal("IT", itManager.DepartmentName);

            var hrManager = managers.First(m => m.FullName == "HR Manager");
            Assert.Equal(2, hrManager.DepartmentId);
            Assert.Equal("HR", hrManager.DepartmentName);
        }

        [Fact]
        public async Task CurrentMonthInfo_ValidId_ReturnsEmployeeInfo()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT", ManagerId = 2 });

            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "Test Employee",
                Email = "employee@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                ManagerId = 2,
                UserId = "user1",
                IsActive = true,
                LeaveBalance = 20
            });

            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567891",
                Role = "Manager",
                DepartmentId = 1,
                UserId = "user2",
                IsActive = true
            });

            var now = DateTime.Now;
            context.LeaveRequests.Add(new LeaveRequest
            {
                EmployeeId = 1,
                StartDate = new DateTime(now.Year, now.Month, 1),
                EndDate = new DateTime(now.Year, now.Month, 3),
                Status = "Approved",
                Reason = "Vacation"
            });

            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("user1", "Employee"));

            // Act
            var result = await controller.CurrentMonthInfo(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic info = okResult.Value;
            Assert.Equal("Test Employee", info.Employee.FullName);
            Assert.Equal("Manager", info.ManagerName);
            Assert.Equal(3, info.DaysOnLeave);
            Assert.Equal(17, info.RemainingLeaveBalance);
        }

        [Fact]
        public async Task Profile_ValidId_ReturnsEmployeeProfile()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT", ManagerId = 2 });

            context.Employees.Add(new Employee
            {
                EmployeeId = 1,
                FullName = "Test Employee",
                Email = "employee@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                ManagerId = 2,
                UserId = "user1",
                IsActive = true
            });

            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567891",
                Role = "Manager",
                DepartmentId = 1,
                UserId = "user2",
                IsActive = true
            });

            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("user1", "Employee"));

            // Act
            var result = await controller.Profile(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic profile = okResult.Value;
            Assert.Equal("Test Employee", profile.Employee.FullName);
            Assert.Equal("Manager", profile.ManagerName);
        }

        [Fact]
        public async Task Profile_InvalidUserId_ReturnsUnauthorized()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "Test Employee",
                Email = "employee@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                UserId = null, // No user ID
                IsActive = true
            };
            context.Employees.Add(employee);
            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("user1", "Employee"));

            // Act
            var result = await controller.Profile(1);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}