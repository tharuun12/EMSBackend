using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System;

public class TrackLastActivityFilterTests
{
    private (DefaultHttpContext context, Mock<ISession> sessionMock, Dictionary<string, byte[]> sessionStore)
        GetHttpContextWithMockSession()
    {
        var sessionMock = new Mock<ISession>();
        var sessionStore = new Dictionary<string, byte[]>();

        sessionMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((key, value) => sessionStore[key] = value);

        var context = new DefaultHttpContext
        {
            Session = sessionMock.Object
        };

        return (context, sessionMock, sessionStore);
    }

    [Fact]
    public void OnActionExecuting_SetsLastActivityTimeInSession()
    {
        var filter = new TrackLastActivityFilter();
        var (httpContext, sessionMock, sessionStore) = GetHttpContextWithMockSession();

        var actionContext = new ActionContext
        {
            HttpContext = httpContext,
            RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
        };

        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object>(),
            controller: null);

        filter.OnActionExecuting(executingContext);

        Assert.True(sessionStore.ContainsKey("LastActivityTime"));
        var storedBytes = sessionStore["LastActivityTime"];
        var storedValue = Encoding.UTF8.GetString(storedBytes);

        Assert.False(string.IsNullOrEmpty(storedValue));
        Assert.True(DateTime.TryParse(storedValue, out _));
    }
}
