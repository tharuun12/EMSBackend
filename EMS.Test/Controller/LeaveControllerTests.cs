using EMS.Models;
using EMS.Web.Controllers;
using EMS.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using static EMS.Web.Controllers.LeaveController;

namespace EMS.Tests.Controller
{
    public class LeaveControllerTests
    {
        // Helper: Create in-memory database for test isolation
        private AppDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        // Helper: Create controller with context, user, and session
        private LeaveController GetController(
            AppDbContext context,
            ClaimsPrincipal? user = null,
            ISession? session = null)
        {
            var controller = new LeaveController(context);
            var httpContext = new DefaultHttpContext();
            if (user != null) httpContext.User = user;
            if (session != null) httpContext.Session = session;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        // Helper: Create ClaimsPrincipal with userId and role
        private ClaimsPrincipal GetUser(string? userId = null, string? role = null)
        {
            var claims = new List<Claim>();
            if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        }

        // Test the Apply (POST) endpoint
        [Fact]
        public async Task Apply_Post_ValidLeaveRequest_ReturnsOk()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 10,
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaveBalance = new LeaveBalance
            {
                EmployeeId = 1,
                TotalLeaves = 10,
                LeavesTaken = 0
            };

            context.Employees.Add(employee);
            context.LeaveBalances.Add(leaveBalance);
            context.SaveChanges();

            var controller = GetController(context, GetUser("user1"));

            var leave = new LeaveRequest
            {
                EmployeeId = 1,
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(3),
                Reason = "Vacation",
                Status = "Pending"
            };

            // Act
            var result = await controller.Apply(leave);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Leave application submitted successfully.", okResult.Value);

            var savedLeave = await context.LeaveRequests.FirstOrDefaultAsync();
            Assert.NotNull(savedLeave);
            Assert.Equal(1, savedLeave.EmployeeId);
            Assert.Equal("Pending", savedLeave.Status);
        }

        [Fact]
        public async Task Apply_Post_InvalidDateRange_ReturnsBadRequest()
        {
            // Arrange
            var context = GetDbContext();
            var controller = GetController(context, GetUser("user1"));

            var leave = new LeaveRequest
            {
                EmployeeId = 1,
                StartDate = DateTime.Today.AddDays(3),
                EndDate = DateTime.Today.AddDays(1), // End date before start date
                Reason = "Vacation",
                Status = "Pending"
            };

            // Act
            var result = await controller.Apply(leave);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("End date must be after start date.", badRequestResult.Value);
        }

        [Fact]
        public async Task Apply_Post_EmployeeNotFound_ReturnsNotFound()
        {
            // Arrange
            var context = GetDbContext();
            var controller = GetController(context, GetUser("user1"));

            var leave = new LeaveRequest
            {
                EmployeeId = 999, // Non-existent employee
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(3),
                Reason = "Vacation",
                Status = "Pending"
            };

            // Act
            var result = await controller.Apply(leave);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Employee not found.", notFoundResult.Value);
        }

        [Fact]
        public async Task Apply_Post_InsufficientLeaveBalance_ReturnsBadRequest()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 2, // Only 2 days available
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaveBalance = new LeaveBalance
            {
                EmployeeId = 1,
                TotalLeaves = 2,
                LeavesTaken = 0
            };

            context.Employees.Add(employee);
            context.LeaveBalances.Add(leaveBalance);
            context.SaveChanges();

            var controller = GetController(context, GetUser("user1"));

            var leave = new LeaveRequest
            {
                EmployeeId = 1,
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(5), // 5 days leave (more than available)
                Reason = "Vacation",
                Status = "Pending"
            };

            // Act
            var result = await controller.Apply(leave);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Insufficient balance", badRequestResult.Value.ToString());
        }

        // Test the MyLeaves endpoint
        [Fact]
        public async Task MyLeaves_ValidEmployeeIdWithRecords_ReturnsOkWithLeaves()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 10,
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaves = new List<LeaveRequest>
            {
                new LeaveRequest
                {
                    LeaveRequestId = 1,
                    EmployeeId = 1,
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(1),
                    Status = "Pending",
                    RequestDate = DateTime.Today
                },
                new LeaveRequest
                {
                    LeaveRequestId = 2,
                    EmployeeId = 1,
                    StartDate = DateTime.Today.AddDays(-10),
                    EndDate = DateTime.Today.AddDays(-8),
                    Status = "Approved",
                    RequestDate = DateTime.Today.AddDays(-15)
                }
            };

            context.Employees.Add(employee);
            context.LeaveRequests.AddRange(leaves);
            context.SaveChanges();

            var controller = GetController(context, GetUser("user1"));

            // Act
            var result = await controller.MyLeaves(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var leavesResult = Assert.IsAssignableFrom<List<LeaveRequest>>(okResult.Value);
            Assert.Equal(2, leavesResult.Count);
            Assert.True(leavesResult[0].RequestDate >= leavesResult[1].RequestDate);
        }

        [Fact]
        public async Task MyLeaves_NoRecords_ReturnsOkWithEmptyList()
        {
            // Arrange
            var context = GetDbContext();
            var controller = GetController(context, GetUser("user1"));

            // Act
            var result = await controller.MyLeaves(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var leavesResult = Assert.IsAssignableFrom<List<LeaveRequest>>(okResult.Value);
            Assert.Empty(leavesResult);
        }

        // Test the ApproveList endpoint
        [Fact]
        public async Task ApproveList_AuthorizedUser_ReturnsPendingLeaves()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 10,
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaves = new List<LeaveRequest>
            {
                new LeaveRequest
                {
                    LeaveRequestId = 1,
                    EmployeeId = 1,
                    Status = "Pending",
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(1)
                },
                new LeaveRequest
                {
                    LeaveRequestId = 2,
                    EmployeeId = 1,
                    Status = "Approved",
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(1)
                }
            };

            context.Employees.Add(employee);
            context.LeaveRequests.AddRange(leaves);
            context.SaveChanges();

            var controller = GetController(context, GetUser("admin", "Admin"));

            // Act
            var result = await controller.ApproveList();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var pendingLeaves = Assert.IsAssignableFrom<List<LeaveRequest>>(okResult.Value);
            Assert.Single(pendingLeaves);
            Assert.Equal("Pending", pendingLeaves[0].Status);
        }

        [Fact]
        public async Task ApproveList_NoPendingLeaves_ReturnsOkWithEmptyList()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 10,
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaves = new List<LeaveRequest>
            {
                new LeaveRequest
                {
                    LeaveRequestId = 1,
                    EmployeeId = 1,
                    Status = "Approved",
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(1)
                },
                new LeaveRequest
                {
                    LeaveRequestId = 2,
                    EmployeeId = 1,
                    Status = "Rejected",
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(1)
                }
            };

            context.Employees.Add(employee);
            context.LeaveRequests.AddRange(leaves);
            context.SaveChanges();

            var controller = GetController(context, GetUser("admin", "Admin"));

            // Act
            var result = await controller.ApproveList();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var pendingLeaves = Assert.IsAssignableFrom<List<LeaveRequest>>(okResult.Value);
            Assert.Empty(pendingLeaves);
        }

        // Test the EmployeeLeaveList endpoint
        [Fact]
        public async Task EmployeeLeaveList_ValidManager_ReturnsTeamPendingLeaves()
        {
            // Arrange
            var context = GetDbContext();

            // Create manager
            var manager = new Employee
            {
                EmployeeId = 1,
                FullName = "Manager",
                Email = "manager@example.com",
                PhoneNumber = "1234567890",
                Role = "Manager",
                IsActive = true,
                DepartmentId = 1,
                UserId = "manager1"
            };

            // Create team members
            var employee1 = new Employee
            {
                EmployeeId = 2,
                FullName = "Employee 1",
                Email = "emp1@example.com",
                PhoneNumber = "1234567891",
                Role = "Employee",
                IsActive = true,
                DepartmentId = 1,
                ManagerId = 1
            };

            var employee2 = new Employee
            {
                EmployeeId = 3,
                FullName = "Employee 2",
                Email = "emp2@example.com",
                PhoneNumber = "1234567892",
                Role = "Employee",
                IsActive = true,
                DepartmentId = 1,
                ManagerId = 1
            };

            // Create leave requests
            var leaveRequests = new List<LeaveRequest>
            {
                new LeaveRequest
                {
                    LeaveRequestId = 1,
                    EmployeeId = 2,
                    Status = "Pending",
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(1)
                },
                new LeaveRequest
                {
                    LeaveRequestId = 2,
                    EmployeeId = 3,
                    Status = "Pending",
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(2)
                },
                new LeaveRequest
                {
                    LeaveRequestId = 3,
                    EmployeeId = 3,
                    Status = "Approved", // Already approved
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(1)
                }
            };

            context.Employees.AddRange(manager, employee1, employee2);
            context.LeaveRequests.AddRange(leaveRequests);
            context.SaveChanges();

            var controller = GetController(context, GetUser("manager1", "Manager"));

            // Act
            var result = await controller.EmployeeLeaveList(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var teamLeaves = Assert.IsAssignableFrom<List<LeaveRequest>>(okResult.Value);
            Assert.Equal(2, teamLeaves.Count);
            Assert.All(teamLeaves, leave => Assert.Equal("Pending", leave.Status));
        }

        [Fact]
        public async Task EmployeeLeaveList_ManagerNotFound_ReturnsNotFound()
        {
            // Arrange
            var context = GetDbContext();
            var controller = GetController(context, GetUser("manager1", "Manager"));

            // Act
            var result = await controller.EmployeeLeaveList(999); // Non-existent manager

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("UserId not found for the given EmployeeId.", notFoundResult.Value);
        }

        // Test the GetLeave endpoint
        [Fact]
        public async Task GetLeave_ValidId_ReturnsLeaveRequest()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 10,
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leave = new LeaveRequest
            {
                LeaveRequestId = 1,
                EmployeeId = 1,
                Status = "Pending",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(1)
            };

            context.Employees.Add(employee);
            context.LeaveRequests.Add(leave);
            context.SaveChanges();

            var controller = GetController(context, GetUser("admin", "Admin"));

            // Act
            var result = await controller.GetLeave(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var leaveResult = Assert.IsType<LeaveRequest>(okResult.Value);
            Assert.Equal(1, leaveResult.LeaveRequestId);
            Assert.Equal("Pending", leaveResult.Status);
        }

        [Fact]
        public async Task GetLeave_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var context = GetDbContext();
            var controller = GetController(context, GetUser("admin", "Admin"));

            // Act
            var result = await controller.GetLeave(999); // Non-existent ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // Test the ApproveLeave endpoint
        [Fact]
        public async Task ApproveLeave_ValidApproval_UpdatesStatusAndBalance()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 10,
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaveBalance = new LeaveBalance
            {
                EmployeeId = 1,
                TotalLeaves = 10,
                LeavesTaken = 0
            };

            var leave = new LeaveRequest
            {
                LeaveRequestId = 1,
                EmployeeId = 1,
                Status = "Pending",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(2) // 3 days leave
            };

            context.Employees.Add(employee);
            context.LeaveBalances.Add(leaveBalance);
            context.LeaveRequests.Add(leave);
            context.SaveChanges();

            var controller = GetController(context, GetUser("admin", "Admin"));

            var request = new ApproveLeaveRequest { Status = "approved" };

            // Act
            var result = await controller.ApproveLeave(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Leave status updated to Approved", okResult.Value.ToString());

            // Check leave status
            var updatedLeave = await context.LeaveRequests.FindAsync(1);
            Assert.Equal("Approved", updatedLeave.Status);

            // Check leave balance
            var updatedBalance = await context.LeaveBalances.FirstOrDefaultAsync(lb => lb.EmployeeId == 1);
            Assert.Equal(3, updatedBalance.LeavesTaken);
        }

        [Fact]
        public async Task ApproveLeave_LeaveNotFound_ReturnsNotFound()
        {
            // Arrange
            var context = GetDbContext();
            var controller = GetController(context, GetUser("admin", "Admin"));

            var request = new ApproveLeaveRequest { Status = "approved" };

            // Act
            var result = await controller.ApproveLeave(999, request); // Non-existent ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ApproveLeave_InsufficientBalance_ReturnsBadRequest()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 2, // Only 2 days available
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaveBalance = new LeaveBalance
            {
                EmployeeId = 1,
                TotalLeaves = 2,
                LeavesTaken = 0
            };

            var leave = new LeaveRequest
            {
                LeaveRequestId = 1,
                EmployeeId = 1,
                Status = "Pending",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(4) // 5 days leave (more than available)
            };

            context.Employees.Add(employee);
            context.LeaveBalances.Add(leaveBalance);
            context.LeaveRequests.Add(leave);
            context.SaveChanges();

            var controller = GetController(context, GetUser("admin", "Admin"));

            var request = new ApproveLeaveRequest { Status = "approved" };

            // Act
            var result = await controller.ApproveLeave(1, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Insufficient balance", badRequestResult.Value.ToString());

            // Check leave status (should still be pending)
            var updatedLeave = await context.LeaveRequests.FindAsync(1);
            Assert.Equal("Pending", updatedLeave.Status);
        }

        [Fact]
        public async Task ApproveLeave_EmptyStatus_ReturnsBadRequest()
        {
            // Arrange
            var context = GetDbContext();
            var controller = GetController(context, GetUser("admin", "Admin"));

            var request = new ApproveLeaveRequest { Status = "" }; // Empty status

            // Act
            var result = await controller.ApproveLeave(1, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Status is required.", badRequestResult.Value);
        }

        [Fact]
        public async Task ApproveLeave_RejectedStatus_UpdatesStatusOnly()
        {
            // Arrange
            var context = GetDbContext();
            var employee = new Employee
            {
                EmployeeId = 1,
                FullName = "John Doe",
                LeaveBalance = 10,
                IsActive = true,
                Email = "john@example.com",
                PhoneNumber = "1234567890",
                Role = "Employee",
                DepartmentId = 1
            };

            var leaveBalance = new LeaveBalance
            {
                EmployeeId = 1,
                TotalLeaves = 10,
                LeavesTaken = 0
            };

            var leave = new LeaveRequest
            {
                LeaveRequestId = 1,
                EmployeeId = 1,
                Status = "Pending",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(2)
            };

            context.Employees.Add(employee);
            context.LeaveBalances.Add(leaveBalance);
            context.LeaveRequests.Add(leave);
            context.SaveChanges();

            var controller = GetController(context, GetUser("admin", "Admin"));

            var request = new ApproveLeaveRequest { Status = "Rejected" };

            // Act
            var result = await controller.ApproveLeave(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Leave status updated to Rejected", okResult.Value.ToString());

            // Check leave status
            var updatedLeave = await context.LeaveRequests.FindAsync(1);
            Assert.Equal("Rejected", updatedLeave.Status);

            // Check leave balance (should not change)
            var updatedBalance = await context.LeaveBalances.FirstOrDefaultAsync(lb => lb.EmployeeId == 1);
            Assert.Equal(0, updatedBalance.LeavesTaken);
        }
    }
}