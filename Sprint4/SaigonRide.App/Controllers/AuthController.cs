using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.App.Models.Entities;
using SaigonRide.App.Models.ViewModels;

namespace SaigonRide.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Find the user by email
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { Message = "Invalid email or password." });
            }

            // 2. Attempt to sign in (false means we don't want a persistent cookie right now)
            var result = await _signInManager.PasswordSignInAsync(user.UserName!, request.Password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // In a full mobile app, you would generate a JWT (JSON Web Token) here.
                // For now, we just confirm the login was successful and return the user data.
                return Ok(new 
                { 
                    Message = "Login successful!", 
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email
                });
            }

            return Unauthorized(new { Message = "Invalid email or password." });
        }
    }
}