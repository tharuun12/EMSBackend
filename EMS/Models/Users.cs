using Microsoft.AspNetCore.Identity;

namespace EMS.Web.Models
{
    public class Users : IdentityUser
    {
        public string? FullName { get; set; }
    }
}
