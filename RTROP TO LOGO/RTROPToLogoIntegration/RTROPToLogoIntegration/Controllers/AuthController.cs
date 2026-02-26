using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RTROPToLogoIntegration.Application.DTOs;
using RTROPToLogoIntegration.Infrastructure.Identity;
using Serilog;
using System.Security.Claims;

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
        /// Kullanıcı girişi. Access token + Refresh token döner.
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
        /// Expired access token + geçerli refresh token ile yeni token çifti alır.
        /// Token rotation uygulanır: eski refresh token geçersiz olur.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto model)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _tokenService.RefreshTokenAsync(model.AccessToken, model.RefreshToken, ipAddress);

            if (result == null)
            {
                return Unauthorized(new { message = "Geçersiz veya süresi dolmuş refresh token." });
            }

            return Ok(new { token = result });
        }

        /// <summary>
        /// Kullanıcının TÜM aktif oturumlarını sonlandırır.
        /// Bu endpoint çağrıldıktan sonra mevcut access token dahil tüm token'lar geçersiz olur.
        /// </summary>
        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _tokenService.RevokeAllTokensAsync(userId);

            return Ok(new { message = "Tüm oturumlar başarıyla sonlandırıldı." });
        }
    }
}
