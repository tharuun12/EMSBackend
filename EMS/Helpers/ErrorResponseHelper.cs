using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace EMS.Helpers
{
    public static class ErrorResponseHelper
    {
        public static Task HandleUnauthorizedAndForbidden(StatusCodeContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = "application/json";

            string message = null;

            if (response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                message = "Unauthorized access. Please login.";
            }
            else if (response.StatusCode == StatusCodes.Status403Forbidden)
            {
                message = "Forbidden: You do not have permission.";
            }

            if (message != null)
            {
                var payload = new { message };
                return response.WriteAsync(JsonSerializer.Serialize(payload));
            }

            return Task.CompletedTask;
        }
    }
}
