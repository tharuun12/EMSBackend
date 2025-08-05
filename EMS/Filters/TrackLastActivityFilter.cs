using Microsoft.AspNetCore.Mvc.Filters;

public class TrackLastActivityFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Session.SetString("LastActivityTime", DateTime.UtcNow.ToString());
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
