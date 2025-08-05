using EMS.Models;
using EMS.Web.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

public class LogUserActivityFilter : IActionFilter
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LogUserActivityFilter(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext.User;

        if (user.Identity.IsAuthenticated)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var url = httpContext.Request.Path;
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString();

            var activity = new UserActivityLog
            {
                UserId = userId,
                UrlAccessed = url,
                ControllerName = controller,
                ActionName = action,
                IPAddress = ip,
                UserAgent = userAgent,
                AccessedAt = DateTime.UtcNow
            };

            _context.UserActivityLogs.Add(activity);
            _context.SaveChanges();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
