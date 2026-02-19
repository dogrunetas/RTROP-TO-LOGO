using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RTROPToLogoIntegration.Application.DTOs;
using RTROPToLogoIntegration.Infrastructure.Identity;
using Serilog;

namespace RTROPToLogoIntegration.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ITokenService _tokenService;

        public AuthController(UserManager<IdentityUser> userManager, ITokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        public class LoginDto
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        /// <summary>
        /// Kullanıcı adı ve şifre ile giriş yapar. Access token ve refresh token döner.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var token = await _tokenService.GenerateTokenAsync(user, ipAddress);

                Log.Information("Kullanıcı başarıyla giriş yaptı. User: {UserName}", model.Username);

                return Ok(new { token });
            }

            Log.Warning("Hatalı giriş denemesi. User: {UserName}", model.Username);
            return Unauthorized();
        }

        /// <summary>
        /// Süresi dolmuş access token + geçerli refresh token ile yeni token seti alır.
        /// Refresh Token Rotation uygulanır: eski refresh token iptal edilir, yenisi üretilir.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto model)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _tokenService.RefreshTokenAsync(model.AccessToken, model.RefreshToken, ipAddress);

            if (result == null)
            {
                Log.Warning("Refresh token isteği reddedildi. IP: {IpAddress}", ipAddress);
                return Unauthorized(new { Message = "Geçersiz veya süresi dolmuş refresh token." });
            }

            return Ok(new { token = result });
        }

        /// <summary>
        /// Mevcut kullanıcının tüm aktif refresh token'larını iptal eder (Logout).
        /// </summary>
        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _tokenService.RevokeAllTokensAsync(userId);

            Log.Information("Kullanıcı tüm token'larını iptal etti. UserId: {UserId}", userId);

            return Ok(new { Message = "Tüm oturumlar başarıyla sonlandırıldı." });
        }
    }
}
