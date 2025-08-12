using EMS.Models;
using EMS.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Xunit;

public class LogUserActivityFilterTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) 
            .Options;

        return new AppDbContext(options);
    }

    private DefaultHttpContext GetHttpContextWithUser()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        httpContext.Request.Path = "/Employee/Index";
        httpContext.Request.Headers["User-Agent"] = "UnitTest-Agent";

        return httpContext;
    }

    [Fact]
    public void OnActionExecuting_AuthenticatedUser_AddsUserActivityLog()
    {
        var dbContext = GetInMemoryDbContext();
        var httpContext = GetHttpContextWithUser();

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        var filter = new LogUserActivityFilter(dbContext, httpContextAccessorMock.Object);

        var actionContext = new ActionContext
        {
            HttpContext = httpContext,
            RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
        };

        actionContext.RouteData.Values["controller"] = "Employee";
        actionContext.RouteData.Values["action"] = "Index";

        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object>(),
            controller: null);

        filter.OnActionExecuting(actionExecutingContext);

        var logs = dbContext.UserActivityLogs.ToListAsync().Result;

        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal("test-user-id", log.UserId);
        Assert.Equal("/Employee/Index", log.UrlAccessed);
        Assert.Equal("Employee", log.ControllerName);
        Assert.Equal("Index", log.ActionName);
        Assert.Equal("127.0.0.1", log.IPAddress);
        Assert.Equal("UnitTest-Agent", log.UserAgent);
        Assert.True((DateTime.UtcNow - log.AccessedAt).TotalSeconds < 10);
    }
}
