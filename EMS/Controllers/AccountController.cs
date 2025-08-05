using EMS.Models;
using EMS.Services.Interface;
using EMS.ViewModels;
using EMS.Web.Data;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static System.Net.WebRequestMethods;

namespace EMS.Web.Controllers
{
    [ApiController]
    [Route("/account")]
    public class AccountApiController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;
        private readonly AppDbContext _context;
        private readonly ILogger<AccountApiController> _logger;

        public AccountApiController(IEmailService emailService, UserManager<Users> userManager,
            SignInManager<Users> signInManager, IConfiguration config,
            AppDbContext context, ILogger<AccountApiController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _emailService = emailService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == model.Email);
            if (employee == null)
                return BadRequest(new { message = "You are not a registered employee. Contact admin." });

            model.FullName = employee.FullName;
            model.PhoneNumber = employee.PhoneNumber;

            var user = new Users
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            var loginLog = new LoginActivityLogs
            {
                userId = user.Id,
                LoginTime = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuccessful = true,
                employeeId = employee.EmployeeId,
                Email = employee.Email
            };

            await _context.LoginActivityLogs.AddAsync(loginLog);
            await _userManager.AddToRoleAsync(user, employee.Role);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Registration successful. Please log in." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Please fill in all required fields." });

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                return Unauthorized(new { message = "Invalid email or password." });

            if (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
                return Forbid("This account is locked. Contact administrator.");

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var token = new JwtSecurityToken(
                issuer: _config["JwtSettings:Issuer"],
                audience: _config["JwtSettings:Audience"],
                expires: DateTime.UtcNow.AddMinutes(60),
                claims: claims,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:Key"])),
                    SecurityAlgorithms.HmacSha256
                )
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == model.Email);
            if (employee != null)
            {
                var log = new LoginActivityLogs
                {
                    userId = user.Id,
                    LoginTime = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    employeeId = employee.EmployeeId,
                    Email = employee.Email
                };

                _context.LoginActivityLogs.Add(log);
                if (string.IsNullOrEmpty(employee.UserId))
                {
                    employee.UserId = user.Id;
                    _context.Employees.Update(employee);
                }
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                token = jwt,
                roles,
                name = user.FullName,
                email = user.Email, 
                employeeId = employee?.EmployeeId
            });
        }

        [HttpPost("forgotpassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return NotFound(new { message = "User not found." });

            var otp = new Random().Next(100000, 999999).ToString();

            HttpContext.Session.SetString("OTP", otp);
            HttpContext.Session.SetString("OTPEmail", model.Email);
            HttpContext.Session.SetString("OTPExpiry", DateTime.UtcNow.AddMinutes(10).ToString());

            await _emailService.SendEmailAsync(model.Email, "EMS Password Reset OTP",
                $"Your OTP is <b>{otp}</b>. Valid for 10 minutes.");

            return Ok(new
            {
                OTP = otp,
                OTPEmail = model.Email,
                OTPExpiry = DateTime.UtcNow.AddMinutes(10).ToString()
            });
        }

        [HttpPost("verify-otp")]
        public IActionResult VerifyOtp([FromBody] VerifyOtpViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Otp) ||
                string.IsNullOrWhiteSpace(model.ExpectedOtp) || string.IsNullOrWhiteSpace(model.OtpExpiry))
            {
                return BadRequest(new { message = "Missing OTP details." });
            }

            if (model.Otp != model.ExpectedOtp)
            {
                return BadRequest(new { message = "Invalid OTP." });
            }

            if (!DateTime.TryParse(model.OtpExpiry, out DateTime expiry) || DateTime.UtcNow > expiry)
            {
                return BadRequest(new { message = "OTP has expired." });
            }

            // OTP is valid
            return Ok(new { message = "OTP verified." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return NotFound(new { message = "User not found." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
                return Ok(new { message = "Password reset successfully." });

            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (result.Succeeded)
                return Ok(new { message = "Password changed successfully." });

            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = _userManager.GetUserId(User);
            var loginRecord = await _context.LoginActivityLogs
                .Where(x => x.userId == userId && x.LogoutTime == null)
                .OrderByDescending(x => x.LoginTime)
                .FirstOrDefaultAsync();

            if (loginRecord != null)
            {
                loginRecord.LogoutTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            Response.Cookies.Delete("jwtToken");

            return Ok(new { message = "Logout successful." });
        }
    }
}
