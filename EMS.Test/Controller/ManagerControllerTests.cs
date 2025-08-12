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
    public class ManagerControllerTests
    {
        private AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        private UserManager<Users> GetMockUserManager()
        {
            var store = new Mock<IUserStore<Users>>();
            return new UserManager<Users>(
                store.Object, null, null, null, null, null, null, null, null);
        }

        private RoleManager<IdentityRole> GetMockRoleManager()
        {
            var store = new Mock<IRoleStore<IdentityRole>>();
            return new RoleManager<IdentityRole>(
                store.Object, null, null, null, null);
        }

        private ManagerController GetControllerWithUser(AppDbContext context, string userId = "user1")
        {
            var roleManager = GetMockRoleManager();
            var userManager = GetMockUserManager();

            var controller = new ManagerController(context, roleManager, userManager);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            return controller;
        }

        [Fact]
        public async Task GetManagerProfile_ReturnsOkWithEmployeeProfile_WhenUserExists()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);
            var employee = new Employee
            {
                UserId = "user1",
                EmployeeId = 1,
                Department = new Department { DepartmentId = 1, DepartmentName = "IT" },
                DepartmentId = 1,
                FullName = "Test User",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                IsActive = true
            };
            context.Employees.Add(employee);
            context.SaveChanges();

            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.GetManagerProfile();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedEmployee = Assert.IsType<Employee>(okResult.Value);
            Assert.Equal(employee.EmployeeId, returnedEmployee.EmployeeId);
        }

        [Fact]
        public async Task GetManagerProfile_ReturnsUnauthorized_WhenUserIdIsNull()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var roleManager = GetMockRoleManager();
            var userManager = GetMockUserManager();

            var controller = new ManagerController(context, roleManager, userManager);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            // Act
            var result = await controller.GetManagerProfile();

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task GetManagerProfile_ReturnsNotFound_WhenEmployeeNotFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.GetManagerProfile();

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Employee not found for current user.", notFound.Value);
        }

        [Fact]
        public async Task GetPendingApprovals_ReturnsNotFound_WhenManagerNotFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.GetPendingApprovals();

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Manager not found.", notFound.Value);
        }

        [Fact]
        public async Task GetPendingApprovals_ReturnsOkWithPendingLeaves()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var manager = new Employee
            {
                EmployeeId = 1,
                UserId = "user1",
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567890",
                Role = "Manager",
                DepartmentId = 1,
                IsActive = true
            };
            var emp1 = new Employee
            {
                EmployeeId = 2,
                ManagerId = 1,
                FullName = "Emp1",
                Email = "emp1@example.com",
                PhoneNumber = "1234567891",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            };
            var emp2 = new Employee
            {
                EmployeeId = 3,
                ManagerId = 1,
                FullName = "Emp2",
                Email = "emp2@example.com",
                PhoneNumber = "1234567892",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            };
            context.Employees.AddRange(manager, emp1, emp2);

            var leave1 = new LeaveRequest { LeaveRequestId = 1, EmployeeId = 2, Status = "Pending", StartDate = DateTime.Today, EndDate = DateTime.Today };
            var leave2 = new LeaveRequest { LeaveRequestId = 2, EmployeeId = 3, Status = "Pending", StartDate = DateTime.Today, EndDate = DateTime.Today };
            var leave3 = new LeaveRequest { LeaveRequestId = 3, EmployeeId = 2, Status = "Approved", StartDate = DateTime.Today, EndDate = DateTime.Today };
            context.LeaveRequests.AddRange(leave1, leave2, leave3);
            context.SaveChanges();

            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.GetPendingApprovals();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var leaves = Assert.IsAssignableFrom<List<LeaveRequest>>(okResult.Value);
            Assert.Equal(2, leaves.Count);
            Assert.All(leaves, l => Assert.Equal("Pending", l.Status));
        }

        [Fact]
        public async Task GetApprovalDetails_ReturnsNotFound_WhenLeaveNotFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.GetApprovalDetails(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetApprovalDetails_ReturnsOk_WhenLeaveFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var employee = new Employee
            {
                EmployeeId = 1,
                UserId = "user1",
                FullName = "Test User",
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            };
            context.Employees.Add(employee);

            var leave = new LeaveRequest
            {
                LeaveRequestId = 1,
                EmployeeId = 1,
                Status = "Pending",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today
            };
            context.LeaveRequests.Add(leave);
            context.SaveChanges();

            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.GetApprovalDetails(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedLeave = Assert.IsType<LeaveRequest>(okResult.Value);
            Assert.Equal(1, returnedLeave.LeaveRequestId);
        }

        [Fact]
        public async Task ApproveOrRejectLeave_ReturnsNotFound_WhenLeaveNotFound()
        {
            // Arrange
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.ApproveOrRejectLeave(999, "Approved");

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetSubordinates_ReturnsOkWithSubordinates()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var manager = new Employee
            {
                EmployeeId = 1,
                UserId = "user1",
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567890",
                Role = "Manager",
                DepartmentId = 1,
                IsActive = true
            };

            var emp1 = new Employee
            {
                EmployeeId = 2,
                ManagerId = 1,
                FullName = "Emp1",
                Email = "emp1@example.com",
                PhoneNumber = "1234567891",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            };

            var emp2 = new Employee
            {
                EmployeeId = 3,
                ManagerId = 1,
                FullName = "Emp2",
                Email = "emp2@example.com",
                PhoneNumber = "1234567892",
                Role = "Employee",
                DepartmentId = 1,
                IsActive = true
            };

            context.Employees.AddRange(manager, emp1, emp2);
            context.SaveChanges();

            var controller = GetControllerWithUser(context);

            // Act
            var result = await controller.GetSubordinates(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var subordinates = Assert.IsAssignableFrom<List<Employee>>(okResult.Value);
            Assert.Equal(2, subordinates.Count);
            Assert.Contains(subordinates, e => e.EmployeeId == 2);
            Assert.Contains(subordinates, e => e.EmployeeId == 3);
        }

    }
}