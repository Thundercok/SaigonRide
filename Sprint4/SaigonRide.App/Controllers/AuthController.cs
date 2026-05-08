using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.App.Models.Entities;
using SaigonRide.App.Models.ViewModels;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace SaigonRide.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            IMemoryCache cache,
            IWebHostEnvironment env)
        {
            _signInManager = signInManager;
            _userManager   = userManager;
            _config        = config;
            _cache         = cache;
            _env           = env;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var user = new ApplicationUser
            {
                UserName  = request.Email,
                Email     = request.Email,
                FullName  = request.FullName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

            return Ok(new { Message = "Registered successfully.", UserId = user.Id });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Unauthorized(new { Message = "Invalid email or password." });

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Unauthorized(new { Message = "Invalid email or password." });

            return Ok(new
            {
                Token    = GenerateJwt(user, hours: 1),
                UserId   = user.Id,
                FullName = user.FullName
            });
        }

        [HttpPost("kiosk-token")]
        [AllowAnonymous]
        public async Task<IActionResult> KioskToken()
        {
            var email    = _config["KioskCredentials:Email"];
            var password = _config["KioskCredentials:Password"];
            if (_env.IsDevelopment())
            {
                email    ??= "kiosk@saigonride.com";
                password ??= "Kiosk@Internal99!";
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Unauthorized();

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return Unauthorized();

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
            if (!result.Succeeded) return Unauthorized();

            return Ok(new { token = GenerateJwt(user, hours: 8) });
        }

[HttpPost("send-otp")]
[AllowAnonymous]
public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
{
    var user = await _userManager.FindByEmailAsync(request.Email);
    if (user == null)
        return NotFound(new { message = "Email chưa được đăng ký." });

    var otp = new Random().Next(100000, 999999).ToString();
    _cache.Set($"otp:{request.Email}", otp, TimeSpan.FromMinutes(5));
    Console.WriteLine($"[OTP] {request.Email} → {otp}");

    var apiKey = _config["Resend:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        await http.PostAsJsonAsync("https://api.resend.com/emails", new
        {
            from    = _config["Resend:From"] ?? "SaigonRide <onboarding@resend.dev>",
            to      = new[] { request.Email },
            subject = "Mã OTP SaigonRide",
            html    = $@"<div style='font-family:sans-serif;max-width:400px'>
                           <h2>SaigonRide</h2>
                           <p>Mã OTP của bạn:</p>
                           <h1 style='letter-spacing:8px;color:#2A5C43'>{otp}</h1>
                           <p style='color:#999'>Hiệu lực trong 5 phút.</p>
                         </div>"
        });
    }

    return Ok(new { message = "OTP đã được gửi." });
}

[HttpPost("verify-otp")]
[AllowAnonymous]
public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
{
    bool otpValid = false;

    if (_env.IsDevelopment() && request.Otp == "123456")
        otpValid = true;
    else if (_cache.TryGetValue($"otp:{request.Email}", out string? stored) && stored == request.Otp)
    {
        otpValid = true;
        _cache.Remove($"otp:{request.Email}");
    }

    if (!otpValid)
        return Unauthorized(new { message = "Mã OTP không đúng hoặc đã hết hạn." });

    var user = await _userManager.FindByEmailAsync(request.Email);
    if (user == null) return Unauthorized();

    return Ok(new { token = GenerateJwt(user, hours: 2), userName = user.FullName });
}

        // ── Shared JWT factory ────────────────────────────────────────────────
        private string GenerateJwt(ApplicationUser user, int hours)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds       = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email,           user.Email  ?? ""),
                new Claim(ClaimTypes.Name,            user.FullName ?? "")
            };

            var token = new JwtSecurityToken(
                issuer:             jwtSettings["Issuer"],
                audience:           jwtSettings["Audience"],
                claims:             claims,
                expires:            DateTime.UtcNow.AddHours(hours),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
            
        }

        public record SendOtpRequest(string Phone);
        public record VerifyOtpRequest(string Phone, string Otp);
    }
}
