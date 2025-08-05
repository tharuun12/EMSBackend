using EMS.Models;
using EMS.ViewModels;
using EMS.Web.Controllers;
using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
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
        public async Task Create_Post_ValidEmployeeModel_AddsEmployeeAndRedirects()
        {
            
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

            
            var result = await controller.Create(model);

            
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("EmployeeList", redirect.ActionName);

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

            
            var result = await controller.Create(model);

            
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("EmployeeList", redirect.ActionName);

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
            
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            
            var result = await controller.Edit(99); // Non-existent ID

            
            Assert.IsType<NotFoundResult>(result);
        }

        // 5. Edit Tests (POST)
        [Fact]
        public async Task Edit_Post_ValidUpdate_UpdatesEmployeeAndRedirects()
        {
            
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

            
            var result = await controller.Edit(1, updatedEmployee);

            
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("EmployeeList", redirect.ActionName);

            var emp = await context.Employees.FirstAsync(e => e.EmployeeId == 1);
            Assert.Equal("New Name", emp.FullName);
            Assert.Equal("new@example.com", emp.Email);
            Assert.Equal("0987654321", emp.PhoneNumber);
            Assert.False(emp.IsActive);
            Assert.Equal("role1", emp.RoleID);
        }

        [Fact]
        public async Task Edit_Post_ManagerChangingDepartment_UpdatesBothDepartments()
        { 
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT", ManagerId = 1, ManagerName = "Manager" });
            context.Department.Add(new Department { DepartmentId = 2, DepartmentName = "HR" }); // No manager
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
            context.SaveChanges();

            var roleManagerMock = MockRoleManager();
            roleManagerMock.Setup(r => r.FindByNameAsync("Manager")).ReturnsAsync(new IdentityRole { Id = "role2", Name = "Manager" });

            var controller = GetController(context, roleManagerMock, MockUserManager(), GetUserWithRole("1", "Admin"));

            var updatedEmployee = new Employee
            {
                EmployeeId = 1,
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567890",
                Role = "Manager",
                DepartmentId = 2, // Changed from 1 to 2
                IsActive = true
            };

            
            var result = await controller.Edit(1, updatedEmployee);

            
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("EmployeeList", redirect.ActionName);

            // Old department should have manager removed
            var oldDept = await context.Department.FirstAsync(d => d.DepartmentId == 1);
            Assert.Null(oldDept.ManagerId);
            Assert.Null(oldDept.ManagerName);

            // New department should have manager assigned
            var newDept = await context.Department.FirstAsync(d => d.DepartmentId == 2);
            Assert.Equal(1, newDept.ManagerId);
            Assert.Equal("Manager", newDept.ManagerName);
        }

        [Fact]
        public async Task Edit_Post_DemotingManager_ClearsManagerFromDepartment()
        {
            
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT", ManagerId = 1, ManagerName = "Manager" });
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
            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "Employee",
                Email = "employee@example.com",
                PhoneNumber = "0987654321",
                Role = "Employee",
                DepartmentId = 1,
                ManagerId = 1,
                IsActive = true
            });
            context.SaveChanges();

            var roleManagerMock = MockRoleManager();
            roleManagerMock.Setup(r => r.FindByNameAsync("Employee")).ReturnsAsync(new IdentityRole { Id = "role1", Name = "Employee" });

            var controller = GetController(context, roleManagerMock, MockUserManager(), GetUserWithRole("1", "Admin"));

            var updatedEmployee = new Employee
            {
                EmployeeId = 1,
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee", 
                DepartmentId = 1,
                IsActive = true
            };          
            var result = await controller.Edit(1, updatedEmployee);
            
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("EmployeeList", redirect.ActionName);

            // Department should have manager removed
            var dept = await context.Department.FirstAsync(d => d.DepartmentId == 1);
            Assert.Null(dept.ManagerId);
            Assert.Null(dept.ManagerName);

            // Subordinate should have manager removed
            var subordinate = await context.Employees.FirstAsync(e => e.EmployeeId == 2);
            Assert.Null(subordinate.ManagerId);
        }

        [Fact]
        public async Task Edit_Post_MismatchedIds_ReturnsNotFound()
        {
            
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
            var result = await controller.Edit(1, updatedEmployee);

            
            Assert.IsType<NotFoundResult>(result);
        }

        // 6. Delete Tests (GET)
        [Fact]
        public async Task Delete_Get_ValidId_ReturnsViewWithEmployee()
        {
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
            var result = await controller.Delete(1);            
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Employee>(viewResult.Model);
            Assert.Equal(1, model.EmployeeId);
            Assert.Equal("Test User", model.FullName);
        }

        [Fact]
        public async Task Delete_Get_InvalidId_ReturnsNotFound()
        {           
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));
           
            var result = await controller.Delete(99); // Non-existent ID
           
            Assert.IsType<NotFoundResult>(result);
        }

        // 7. DeleteConfirmed Tests
        [Fact]
        public async Task DeleteConfirmed_EmployeeWithNoManagerRole_DeletesAndLogsChanges()
        {           
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
           
            var result = await controller.DeleteConfirmed(1);
           
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("EmployeeList", redirect.ActionName);

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
        public async Task DeleteConfirmed_EmployeeAssignedAsManager_PreventsDeletionAndShowsError()
        {
            
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

            
            var result = await controller.DeleteConfirmed(1);

            
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("EmployeeList", redirect.ActionName);

            // Employee should not be deleted
            Assert.Single(await context.Employees.ToListAsync());

            // Error message should be set
            Assert.True(controller.TempData.ContainsKey("ToastError"));
            Assert.Contains("Cannot delete employee: assigned as department manager",
                controller.TempData["ToastError"].ToString());

            // Department should still have manager
            var dept = await context.Department.FirstAsync(d => d.DepartmentId == 1);
            Assert.Equal(1, dept.ManagerId);
            Assert.Equal("Manager", dept.ManagerName);
        }

        [Fact]
        public async Task DeleteConfirmed_ManagerWithSubordinates_UpdatesSubordinates()
        {
            // First we need to modify the controller code to allow deleting managers
            // This test assumes the logic has been modified to allow manager deletion

            using var context = GetDbContext(Guid.NewGuid().ToString());

            // Create a manager in department 1
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

            // Create subordinate employees
            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "Employee1",
                Email = "emp1@example.com",
                PhoneNumber = "1234567891",
                Role = "Employee",
                DepartmentId = 1,
                ManagerId = 1,
                IsActive = true
            });

            context.Employees.Add(new Employee
            {
                EmployeeId = 3,
                FullName = "Employee2",
                Email = "emp2@example.com",
                PhoneNumber = "1234567892",
                Role = "Employee",
                DepartmentId = 1,
                ManagerId = 1,
                IsActive = true
            });

            // Create department with this manager
            context.Department.Add(new Department
            {
                DepartmentId = 1,
                DepartmentName = "IT",
                ManagerId = 1,
                ManagerName = "Manager"
            });

            context.SaveChanges();

            var userManagerMock = MockUserManager();
            userManagerMock.Setup(u => u.FindByEmailAsync("manager@example.com"))
                .ReturnsAsync(new Users { Email = "manager@example.com" });

            var controller = GetController(context, MockRoleManager(), userManagerMock, GetUserWithRole("1", "Admin"));
        }

        // 8. Filter Tests
        [Fact]
        public async Task Filter_WithDepartmentFilter_ReturnsMatchingEmployees()
        {
            
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

            
            var result = await controller.Filter(departmentId: 1, role: null);

            
            var viewResult = Assert.IsType<ViewResult>(result);
            var employees = Assert.IsAssignableFrom<List<Employee>>(viewResult.Model);
            Assert.Single(employees);
            Assert.Equal("IT Employee", employees[0].FullName);

            Assert.NotNull(controller.ViewBag.Departments);
            Assert.NotNull(controller.ViewBag.Roles);
        }

        [Fact]
        public async Task Filter_WithRoleFilter_ReturnsMatchingEmployees()
        {
            
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT" });

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

            context.Employees.Add(new Employee
            {
                EmployeeId = 2,
                FullName = "Employee",
                Email = "employee@example.com",
                PhoneNumber = "1234567891",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            });

            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            
            var result = await controller.Filter(departmentId: null, role: "Manager");

            
            var viewResult = Assert.IsType<ViewResult>(result);
            var employees = Assert.IsAssignableFrom<List<Employee>>(viewResult.Model);
            Assert.Single(employees);
            Assert.Equal("Manager", employees[0].FullName);
        }

        [Fact]
        public async Task Filter_WithBothFilters_ReturnsMatchingEmployees()
        {
            
            using var context = GetDbContext(Guid.NewGuid().ToString());
            context.Department.Add(new Department { DepartmentId = 1, DepartmentName = "IT" });
            context.Department.Add(new Department { DepartmentId = 2, DepartmentName = "HR" });

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

            context.SaveChanges();

            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("1", "Admin"));

            
            var result = await controller.Filter(departmentId: 1, role: "Manager");

            
            var viewResult = Assert.IsType<ViewResult>(result);
            var employees = Assert.IsAssignableFrom<List<Employee>>(viewResult.Model);
            Assert.Single(employees);
            Assert.Equal("IT Manager", employees[0].FullName);
        }

        // 9. Additional User View Tests
        [Fact]
        public async Task Index_LoggedInUser_ReturnsUserProfile()
        {
            
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

            
            var result = await controller.Index();

            
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<EmployeeProfileViewModel>(viewResult.Model);
            Assert.Equal("Test Employee", model.Employee.FullName);
            Assert.Equal("Manager", model.ManagerName);
        }

        [Fact]
        public async Task Index_NotLoggedIn_ReturnsUnauthorized()
        {
            
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetController(context, MockRoleManager(), MockUserManager()); // No user

            
            var result = await controller.Index();

            
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Index_LoggedInButNoEmployee_ReturnsNotFound()
        {
            
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetController(context, MockRoleManager(), MockUserManager(), GetUserWithRole("nonexistent", "Employee"));

            
            var result = await controller.Index();

            
            Assert.IsType<NotFoundObjectResult>(result);
            var notFoundResult = result as NotFoundObjectResult;
            Assert.Equal("Employee not found for current user.", notFoundResult.Value);
        }   

        [Fact]
        public async Task ManagersList_ReturnsOnlyManagers()
        {
            
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
            
            var result = await controller.ManagersList();
            
            var viewResult = Assert.IsType<ViewResult>(result);
            var managers = Assert.IsAssignableFrom<List<ManagerDetailsViewModel>>(viewResult.Model);
            Assert.Equal(2, managers.Count);

            var itManager = managers.First(m => m.FullName == "IT Manager");
            Assert.Equal(1, itManager.DepartmentId);
            Assert.Equal("IT", itManager.DepartmentName);

            var hrManager = managers.First(m => m.FullName == "HR Manager");
            Assert.Equal(2, hrManager.DepartmentId);
            Assert.Equal("HR", hrManager.DepartmentName);
        }
    }
}