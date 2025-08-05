using EMS.Controllers;
using EMS.Models;
using EMS.ViewModels;
using EMS.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EMS.Tests.Controllers
{
    public class DashboardControllerTests
    {
        private AppDbContext GetDbContextWithTestData()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) 
                .Options;

            var context = new AppDbContext(options);

            var hrDept = new Department { DepartmentId = 1, DepartmentName = "HR", Employees = new List<Employee>() };
            var itDept = new Department { DepartmentId = 2, DepartmentName = "IT", Employees = new List<Employee>() };
            context.Department.AddRange(hrDept, itDept);

            var employees = new List<Employee>
                {
                    new Employee
                    {
                        EmployeeId = 1,
                        FullName = "Alice",
                        IsActive = true,
                        Email = "alice@example.com",
                        PhoneNumber = "1111111111",
                        Role = "Employee",
                        Department = hrDept
                    },
                    new Employee
                    {
                        EmployeeId = 2,
                        FullName = "Bob",
                        IsActive = false,
                        Email = "bob@example.com",
                        PhoneNumber = "2222222222",
                        Role = "Manager",
                        Department = itDept
                    },
                    new Employee
                    {
                        EmployeeId = 3,
                        FullName = "Charlie",
                        IsActive = true,
                        Email = "charlie@example.com",
                        PhoneNumber = "3333333333",
                        Role = "Employee",
                        Department = hrDept
                    },
                    new Employee
                    {
                        EmployeeId = 4,
                        FullName = "David",
                        IsActive = true,
                        Email = "david@example.com",
                        PhoneNumber = "4444444444",
                        Role = "Employee",
                        Department = itDept
                    },
                    new Employee
                    {
                        EmployeeId = 5,
                        FullName = "Eva",
                        IsActive = false,
                        Email = "eva@example.com",
                        PhoneNumber = "5555555555",
                        Role = "HR",
                        Department = hrDept
                    }
                };

            context.Employees.AddRange(employees);

            context.SaveChanges();
            return context;
        }

        [Fact]
        public async Task Index_ReturnsCorrectDashboardAnalytics()
        {
            var context = GetDbContextWithTestData();
            var controller = new DashboardController(context);

            var result = await controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<DepartmentStatsViewModel>>(viewResult.Model);

            Assert.Equal(5, controller.ViewBag.TotalEmployees);
            Assert.Equal(3, controller.ViewBag.ActiveEmployees);
            Assert.Equal(2, controller.ViewBag.TotalDepartments);

            var recent = controller.ViewBag.RecentEmployees as List<Employee>;
            Assert.NotNull(recent);
            Assert.Equal(5, recent.Count);

            Assert.Equal(2, model.Count);
            var hrStats = model.FirstOrDefault(d => d.Name == "HR");
            var itStats = model.FirstOrDefault(d => d.Name == "IT");

            Assert.NotNull(hrStats);
            Assert.NotNull(itStats);

            Assert.Equal(3, hrStats.EmployeeCount);  
            Assert.Equal(2, itStats.EmployeeCount);  
        }
    }
}
