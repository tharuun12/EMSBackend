using EMS.Web.Controllers;
using EMS.Models;
using EMS.Services.Interface;
using EMS.ViewModels;
using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EMS.Tests.Controller
{
    public class AccountApiControllerTests
    {
        private static AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        private static AccountApiController GetController(
            AppDbContext context,
            Mock<UserManager<Users>> userManagerMock,
            Mock<SignInManager<Users>> signInManagerMock,
            Mock<IEmailService> emailServiceMock,
            Mock<IConfiguration> configMock,
            Mock<ILogger<AccountApiController>> loggerMock,
            ISession session = null)
        {
            var controller = new AccountApiController(
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
        public async Task Register_Post_ReturnsBadRequest_WhenEmployeeNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountApiController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);
            var model = new RegisterViewModel
            {
                Email = "notfound@example.com",
                Password = "Password1!",
                ConfirmPassword = "Password1!"
            };

            var result = await controller.Register(model);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("You are not a registered employee", badRequestResult.Value.ToString());
        }

        [Fact]
        public async Task Login_Post_ReturnsUnauthorized_WhenUserNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((Users)null);

            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountApiController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);
            var model = new LoginViewModel
            {
                Email = "notfound@example.com",
                Password = "Password1!"
            };

            var result = await controller.Login(model);

            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Contains("Invalid email or password", unauthorizedResult.Value.ToString());
        }

        [Fact]
        public async Task ForgotPassword_Post_ReturnsNotFound_WhenUserNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((Users)null);

            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountApiController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);
            var model = new ForgotPasswordViewModel
            {
                Email = "notfound@example.com"
            };

            var result = await controller.ForgotPassword(model);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("User not found", notFoundResult.Value.ToString());
        }

        [Fact]
        public async Task ResetPassword_Post_ReturnsNotFound_WhenUserNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((Users)null);

            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountApiController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);
            var model = new ResetPasswordViewModel
            {
                Email = "notfound@example.com",
                NewPassword = "NewPassword1!",
                ConfirmPassword = "NewPassword1!"
            };

            var result = await controller.ResetPassword(model);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("User not found", notFoundResult.Value.ToString());
        }

        [Fact]
        public async Task ChangePassword_Post_ReturnsUnauthorized_WhenUserNotFound()
        {
            using var context = GetDbContext(Guid.NewGuid().ToString());
            var userManagerMock = MockUserManager();
            userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((Users)null);

            var signInManagerMock = MockSignInManager(userManagerMock.Object);
            var emailServiceMock = new Mock<IEmailService>();
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AccountApiController>>();

            var controller = GetController(context, userManagerMock, signInManagerMock, emailServiceMock, configMock, loggerMock);
            var model = new ChangePasswordViewModel
            {
                Email = "notfound@example.com",
                OldPassword = "OldPassword1!",
                NewPassword = "NewPassword1!",
                ConfirmPassword = "NewPassword1!"
            };

            var result = await controller.ChangePassword(model);

            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        }
    }
}