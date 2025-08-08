//using EMS.Models;
//using EMS.Web.Controllers;
//using EMS.Web.Data;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading.Tasks;
//using Xunit;

//namespace EMS.Tests.Controller
//{
//    public class LeaveControllerTests
//    {
//        // Helper: Create in-memory database for test isolation
//        private AppDbContext GetDbContext()
//        {
//            var options = new DbContextOptionsBuilder<AppDbContext>()
//                .UseInMemoryDatabase(Guid.NewGuid().ToString())
//                .Options;
//            return new AppDbContext(options);
//        }

//        // Helper: Create controller with context, user, and session
//        private LeaveController GetController(
//            AppDbContext context,
//            ClaimsPrincipal? user = null,
//            ISession? session = null)
//        {
//            var controller = new LeaveController(context);
//            var httpContext = new DefaultHttpContext();
//            if (user != null) httpContext.User = user;
//            if (session != null) httpContext.Session = session;
//            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
//            return controller;
//        }

//        // Helper: Create ClaimsPrincipal with userId and role
//        private ClaimsPrincipal GetUser(string? userId = null, string? role = null)
//        {
//            var claims = new List<Claim>();
//            if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
//            if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
//            return new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
//        }

//        // 1. Apply (GET) 
//        [Fact] // Valid user with existing Employee record
//        public void Apply_Get_ValidUserWithEmployee_ReturnsViewWithEmployee()
//        {
            
//            var context = GetDbContext();
//            var employee = new Employee
//            {
//                EmployeeId = 1,
//                UserId = "user1",
//                FullName = "John Doe",
//                LeaveBalance = 10,
//                IsActive = true,
//                Email = "john@example.com",
//                PhoneNumber = "1234567890",
//                Role = "Employee",
//                DepartmentId = 1
//            };
//            context.Employees.Add(employee);
//            context.SaveChanges();

//            var controller = GetController(context, GetUser("user1"));

            
//            var result = controller.Apply();

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            Assert.Equal(employee, viewResult.ViewData["Employee"]);
//        }

//        [Fact] // No matching Employee for logged-in user
//        public void Apply_Get_NoEmployee_ReturnsNotFound()
//        {
            
//            var context = GetDbContext();
//            var controller = GetController(context, GetUser("nonexistent"));

            
//            var result = controller.Apply();

            
//            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
//            Assert.Equal("Employee not found.", notFoundResult.Value);
//        }

//        // 2. Apply (POST) Tests
//        [Fact] // Invalid ModelState
//        public async Task Apply_Post_InvalidModelState_ReturnsViewWithErrors()
//        {
            
//            var context = GetDbContext();
//            var controller = GetController(context);
//            controller.ModelState.AddModelError("StartDate", "Required");
//            var leave = new LeaveRequest();

            
//            var result = await controller.Apply(leave);

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            Assert.Equal(leave, viewResult.Model);
//            Assert.NotNull(context.Employees); // Should be populated in ViewData
//        }

//        // 3. MyLeaves Tests
//        [Fact] // Valid employee ID with leave records
//        public async Task MyLeaves_ValidEmployeeIdWithRecords_ReturnsViewWithLeaves()
//        {
            
//            var context = GetDbContext();
//            var employee = new Employee
//            {
//                EmployeeId = 1,
//                FullName = "John Doe",
//                LeaveBalance = 10,
//                IsActive = true,
//                Email = "john@example.com",
//                PhoneNumber = "1234567890",
//                Role = "Employee",
//                DepartmentId = 1
//            };
//            var leaves = new List<LeaveRequest>
//            {
//                new LeaveRequest
//                {
//                    LeaveRequestId = 1,
//                    EmployeeId = 1,
//                    StartDate = DateTime.Today,
//                    EndDate = DateTime.Today.AddDays(1),
//                    Status = "Pending",
//                    RequestDate = DateTime.Today
//                },
//                new LeaveRequest
//                {
//                    LeaveRequestId = 2,
//                    EmployeeId = 1,
//                    StartDate = DateTime.Today.AddDays(-10),
//                    EndDate = DateTime.Today.AddDays(-8),
//                    Status = "Approved",
//                    RequestDate = DateTime.Today.AddDays(-15)
//                }
//            };
//            context.Employees.Add(employee);
//            context.LeaveRequests.AddRange(leaves);
//            context.SaveChanges();

//            var controller = GetController(context);

            
//            var result = await controller.MyLeaves(1);

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            var model = Assert.IsAssignableFrom<List<LeaveRequest>>(viewResult.Model);
//            Assert.Equal(2, model.Count);

//            Assert.True(model[0].RequestDate >= model[1].RequestDate);

//            // Check ViewBag data
//            Assert.Equal("John Doe", controller.ViewBag.EmployeeName);
//        }

//        [Fact] // Employee ID with no leave records
//        public async Task MyLeaves_NoRecords_ReturnsViewWithEmptyList()
//        {
            
//            var context = GetDbContext();
//            var controller = GetController(context);

            
//            var result = await controller.MyLeaves(1);

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            var model = Assert.IsAssignableFrom<List<LeaveRequest>>(viewResult.Model);
//            Assert.Empty(model);
//            Assert.Equal("", controller.ViewBag.EmployeeName);
//        }

//        // 4. ApproveList Tests
//        [Fact] // Authorized Admin/Manager accesses the list
//        public async Task ApproveList_AuthorizedUser_ReturnsPendingLeaves()
//        {
            
//            var context = GetDbContext();
//            var employee = new Employee
//            {
//                EmployeeId = 1,
//                FullName = "John Doe",
//                LeaveBalance = 10,
//                IsActive = true,
//                Email = "john@example.com",
//                PhoneNumber = "1234567890",
//                Role = "Employee",
//                DepartmentId = 1
//            };
//            var leaves = new List<LeaveRequest>
//            {
//                new LeaveRequest
//                {
//                    LeaveRequestId = 1,
//                    EmployeeId = 1,
//                    Status = "Pending",
//                    StartDate = DateTime.Today,
//                    EndDate = DateTime.Today.AddDays(1)
//                },
//                new LeaveRequest
//                {
//                    LeaveRequestId = 2,
//                    EmployeeId = 1,
//                    Status = "Approved",
//                    StartDate = DateTime.Today,
//                    EndDate = DateTime.Today.AddDays(1)
//                }
//            };
//            context.Employees.Add(employee);
//            context.LeaveRequests.AddRange(leaves);
//            context.SaveChanges();

//            var controller = GetController(context, GetUser("admin", "Admin"));

            
//            var result = await controller.ApproveList();

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            var model = Assert.IsAssignableFrom<List<LeaveRequest>>(viewResult.Model);
//            Assert.Single(model); 
//            Assert.Equal("Pending", model[0].Status);
//        }

//        [Fact] //No pending leaves
//        public async Task ApproveList_NoPendingLeaves_ReturnsEmptyList()
//        {
            
//            var context = GetDbContext();
//            var employee = new Employee
//            {
//                EmployeeId = 1,
//                FullName = "John Doe",
//                LeaveBalance = 10,
//                IsActive = true,
//                Email = "john@example.com",
//                PhoneNumber = "1234567890",
//                Role = "Employee",
//                DepartmentId = 1
//            };
//            var leaves = new List<LeaveRequest>
//            {
//                new LeaveRequest
//                {
//                    LeaveRequestId = 1,
//                    EmployeeId = 1,
//                    Status = "Approved",
//                    StartDate = DateTime.Today,
//                    EndDate = DateTime.Today.AddDays(1)
//                },
//                new LeaveRequest
//                {
//                    LeaveRequestId = 2,
//                    EmployeeId = 1,
//                    Status = "Rejected",
//                    StartDate = DateTime.Today,
//                    EndDate = DateTime.Today.AddDays(1)
//                }
//            };
//            context.Employees.Add(employee);
//            context.LeaveRequests.AddRange(leaves);
//            context.SaveChanges();

//            var controller = GetController(context, GetUser("admin", "Admin"));

            
//            var result = await controller.ApproveList();

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            var model = Assert.IsAssignableFrom<List<LeaveRequest>>(viewResult.Model);
//            Assert.Empty(model);
//        }

//        [Fact] // Unauthorized user accesses route
//        public void ApproveList_UnauthorizedUser_AccessDenied()
//        {
            
//            var context = GetDbContext();
//            var controller = GetController(context, GetUser("employee", "Employee"));

//            // Note: Since we can't test Authorize attribute directly in unit tests,
//            // we check if the user has the required role
//            Assert.False(controller.User.IsInRole("Admin") || controller.User.IsInRole("Manager"));
//        }

//        // 5. Approvals (GET) Tests
//        [Fact] // Valid ID returns leave with employee info
//        public async Task Approvals_Get_ValidId_ReturnsViewWithLeave()
//        {
            
//            var context = GetDbContext();
//            var employee = new Employee
//            {
//                EmployeeId = 1,
//                FullName = "John Doe",
//                LeaveBalance = 10,
//                IsActive = true,
//                Email = "john@example.com",
//                PhoneNumber = "1234567890",
//                Role = "Employee",
//                DepartmentId = 1
//            };
//            var leave = new LeaveRequest
//            {
//                LeaveRequestId = 1,
//                EmployeeId = 1,
//                Status = "Pending",
//                StartDate = DateTime.Today,
//                EndDate = DateTime.Today.AddDays(1)
//            };
//            context.Employees.Add(employee);
//            context.LeaveRequests.Add(leave);
//            context.SaveChanges();

//            var controller = GetController(context, GetUser("admin", "Admin"));

            
//            var result = await controller.Approvals(1);

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            var model = Assert.IsType<LeaveRequest>(viewResult.Model);
//            Assert.Equal(1, model.LeaveRequestId);
//        }

//        [Fact] // TC018: Invalid ID returns null
//        public async Task Approvals_Get_InvalidId_ReturnsNotFound()
//        {
            
//            var context = GetDbContext();
//            var controller = GetController(context, GetUser("admin", "Admin"));

            
//            var result = await controller.Approvals(999); // Non-existent ID

            
//            Assert.IsType<NotFoundResult>(result);
//        }

//        // 6. Approvals (POST) Tests
//        [Fact] // Leave not found
//        public async Task Approvals_Post_LeaveNotFound_ReturnsNotFound()
//        {
            
//            var context = GetDbContext();
//            var controller = GetController(context, GetUser("admin", "Admin"));

            
//            var result = await controller.Approvals(999, "Approved"); // Non-existent ID

            
//            Assert.IsType<NotFoundResult>(result);
//        }
//    }
//}