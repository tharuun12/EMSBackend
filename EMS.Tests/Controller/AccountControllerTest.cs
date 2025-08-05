using EMS.Controllers;
using EMS.Models;
using EMS.Services.Interface;
using EMS.ViewModels;
using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace EMS.Tests.Controller
{
    public class AccountControllerTests
    {
        private static AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        private static AccountController GetController(
            AppDbContext context,
            Mock<UserManager<Users>> userManagerMock,
            Mock<SignInManager<Users>> signInManagerMock,
            Mock<IEmailService> emailServiceMock,
            Mock<IConfiguration> configMock,
            Mock<ILogger<AccountController>> loggerMock,
            ISession session = null)
        {
            var controller = new AccountController(
                emailServiceMock.Object,
                userManagerMock.Object,
                signInManagerMock.Object,
                configMock.Object,
                context,
                loggerMock.Object
            );

            var httpContext = new DefaultHttpContext();
            httpContext.Session = session ?? new TestSession();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            return controller;
        }

        private static Mock<UserManager<Users>> MockUserManager()
        {
            var store = new Mock<IUserStore<Users>>();
            return new Mock<UserManager<Users>>(
                store.Object, null, null, null, null, null, null, null, null
            );
        }

        private static Mock<SignInManager<Users>> MockSignInManager(UserManager<Users> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<Users>>();
            return new Mock<SignInManager<Users>>(
                userManager, contextAccessor.Object, claimsFactory.Object, null, null, null, null
            );
        }

        private class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _sessionStorage = new();
            public IEnumerable<string> Keys => _sessionStorage.Keys;
            public string Id => Guid.NewGuid().ToString();
            public bool IsAvailable => true;
            public void Clear() => _sessionStorage.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => _sessionStorage.Remove(key);
            public void Set(string key, byte[] value) => _sessionStorage[key] = value;
            public bool TryGetValue(string key, out byte[] value) => _sessionStorage.TryGetValue(key, out value);
        }

        [Fact]
        public void Login_Get_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.Login();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void Register_Get_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.Register();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Register_Post_ReturnsView_WhenEmployeeNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);
            controller.TempData = new TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<ITempDataProvider>()
            );
            var model = new RegisterViewModel
            {
                Email = "notfound@example.com",
                Password = "Password1!",
                ConfirmPassword = "Password1!",
                FullName = "Test User",
                PhoneNumber = "1234567890"
            };

            var result = await controller.Register(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
            Assert.True(controller.ModelState.ErrorCount > 0);
        }

        [Fact]
        public async Task Login_Post_ReturnsView_WhenUserNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((Users)null);

            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);
            controller.TempData = new TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<ITempDataProvider>()
            );
            var model = new LoginViewModel
            {
                Email = "notfound@example.com",
                Password = "Password1!"
            };

            var result = await controller.Login(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
            Assert.True(controller.ModelState.ErrorCount > 0);
        }

        [Fact]
        public async Task ForgotPassword_Post_RedirectsToConfirmation_WhenUserNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((Users)null);

            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);

            var model = new ForgotPasswordViewModel
            {
                Email = "notfound@example.com"
            };

            var result = await controller.ForgotPassword(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ForgotPasswordNotFound", redirect.ActionName);
        }

        [Fact]
        public void ForgotPassword_Get_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.ForgotPassword();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void VerifyOtp_Get_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.VerifyOtp("test@example.com");
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<VerifyOtpViewModel>(viewResult.Model);
        }

        [Fact]
        public void ResetPassword_Get_Redirects_WhenTempDataMissing()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
            var result = controller.ResetPassword();
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ForgotPassword", redirect.ActionName);
        }

        [Fact]
        public void ChangePassword_Get_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.ChangePassword();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void ChangePasswordConfirmation_Get_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.ChangePasswordConfirmation();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void ForgotPasswordConfirmation_Get_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.ForgotPasswordConfirmation();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void AdminDashboard_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.AdminDashboard();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void ManagerDashboard_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.ManagerDashboard();
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void EmployeeDashboard_ReturnsView()
        {
            var controller = GetController(GetDbContext(Guid.NewGuid().ToString()), MockUserManager(), MockSignInManager(MockUserManager().Object), new Mock<IEmailService>(), new Mock<IConfiguration>(), new Mock<ILogger<AccountController>>());
            var result = controller.EmployeeDashboard();
            Assert.IsType<ViewResult>(result);
        }
    }
}