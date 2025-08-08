//using EMS.Models;
//using EMS.Web.Controllers;
//using EMS.Web.Data;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.ViewFeatures;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Xunit;

//namespace EMS.Tests.Controllers
//{
//    public class DepartmentControllerTests
//    {
//        private async Task<AppDbContext> GetInMemoryDbContextAsync()
//        {
//            var options = new DbContextOptionsBuilder<AppDbContext>()
//                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
//                .Options;

//            var context = new AppDbContext(options);

//            // Seed sample manager
//            var manager = new Employee
//            {
//                EmployeeId = 1,
//                FullName = "Alice Johnson",
//                Email = "alice@example.com",
//                PhoneNumber = "1234567890",
//                Role = "Manager",
//            };
//            await context.Employees.AddAsync(manager);

//            // Seed department
//            var dept = new Department
//            {
//                DepartmentId = 1,
//                DepartmentName = "IT",
//                ManagerId = 1,
//                ManagerName = "Alice Johnson"
//            };
//            await context.Department.AddAsync(dept);

//            await context.SaveChangesAsync();
//            return context;
//        }
//        private RoleManager<IdentityRole> GetMockRoleManager()
//        {
//            var store = new Mock<IRoleStore<IdentityRole>>();
//            return new RoleManager<IdentityRole>(
//                store.Object,
//                null, null, null, null);
//        }


//        private DepartmentController SetupControllerWithContext(AppDbContext context)
//        {
//            var roleManager = GetMockRoleManager();
//            var controller = new DepartmentController(context, roleManager);



//            var tempData = new TempDataDictionary(
//                new DefaultHttpContext(),
//                Mock.Of<ITempDataProvider>()
//            );
//            controller.TempData = tempData;

//            return controller;
//        }


//        [Fact]
//        public async Task Index_ReturnsViewWithDepartments()
//        {
            
//            var context = await GetInMemoryDbContextAsync();
//            var roleManager = GetMockRoleManager();
//            var controller = new DepartmentController(context, roleManager);

            
//            var result = await controller.Index();

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            var model = Assert.IsAssignableFrom<IEnumerable<Department>>(viewResult.Model);
//            Assert.Single(model);
//        }

//        [Fact]
//        public async Task Create_Get_ReturnsViewWithManagers()
//        {
            
//            var context = await GetInMemoryDbContextAsync();
//            var roleManager = GetMockRoleManager();
//            var controller = new DepartmentController(context, roleManager);

            
//            var result = controller.Create();

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            Assert.NotNull(viewResult.ViewData["Managers"]);
//        }

        
//        [Fact]
//        public async Task Edit_Get_ReturnsDepartmentForEdit()
//        {
            
//            var context = await GetInMemoryDbContextAsync();
//            var roleManager = GetMockRoleManager();
//            var controller = new DepartmentController(context, roleManager);

            
//            var result = await controller.Edit(1);

            
//            var viewResult = Assert.IsType<ViewResult>(result);
//            var model = Assert.IsType<Department>(viewResult.Model);
//            Assert.Equal(1, model.DepartmentId);
//        }

//        [Fact]
//        public async Task Edit_Post_ValidUpdate_LogsAndUpdates()
//        {
            
//            var context = await GetInMemoryDbContextAsync();
//            var controller = SetupControllerWithContext(context);

//            var trackedDept = await context.Department.FindAsync(1);
//            context.Entry(trackedDept).State = EntityState.Detached;

//            var updatedDept = new Department
//            {
//                DepartmentId = 1,
//                DepartmentName = "Updated IT",
//                ManagerId = 1,
//                ManagerName = "Alice Johnson"
//            };

            
//            var result = await controller.Edit(1, updatedDept);

            
//            var redirect = Assert.IsType<RedirectToActionResult>(result);
//            Assert.Equal("Index", redirect.ActionName);

//            var updated = await context.Department.FindAsync(1);
//            Assert.Equal("Updated IT", updated.DepartmentName);

//            var logs = await context.departmentLogs.ToListAsync();
//            Assert.Single(logs);

//            var log = logs.First();
//            Assert.Equal("Updated IT", log.DepartmentName);
//            Assert.Equal("Updated", log.Operation);
//        }


//        [Fact]
//        public async Task DeleteConfirmed_RemovesDepartmentAndLogs_WhenNoEmployees()
//        {
            
//            var context = await GetInMemoryDbContextAsync();
//            var controller = SetupControllerWithContext(context);

            
//            var result = await controller.DeleteConfirmed(1);

            
//            var redirect = Assert.IsType<RedirectToActionResult>(result);
//            Assert.Equal("Index", redirect.ActionName);

//            Assert.Empty(await context.Department.ToListAsync());
//            Assert.Single(await context.departmentLogs.ToListAsync());
//        }

//        [Fact]
//        public async Task DeleteConfirmed_PreventsDeletion_WhenEmployeesExist()
//        {
            
//            var context = await GetInMemoryDbContextAsync();
//            var controller = SetupControllerWithContext(context);

//            context.Employees.Add(new Employee
//            {
//                EmployeeId = 99, 
//                FullName = "Test Employee",
//                Email = "test@example.com",
//                PhoneNumber = "1234567890",
//                Role = "Developer",
//                DepartmentId = 1
//            });
//            await context.SaveChangesAsync();

            
//            var result = await controller.DeleteConfirmed(1);

            
//            var redirect = Assert.IsType<RedirectToActionResult>(result);
//            Assert.Equal("Index", redirect.ActionName);

//            Assert.True(controller.TempData.ContainsKey("ToastError"));
//            Assert.NotEmpty(await context.Department.ToListAsync());
//        }

//    }
//}
