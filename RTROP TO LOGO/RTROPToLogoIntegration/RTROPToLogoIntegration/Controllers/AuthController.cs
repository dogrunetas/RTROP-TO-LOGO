using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var token = await _tokenService.GenerateTokenAsync(user);
                
                Log.Information("Kullanıcı başarıyla giriş yaptı. User: {UserName}", model.Username);
                
                return Ok(new { token });
            }

            Log.Warning("Hatalı giriş denemesi. User: {UserName}", model.Username);
            return Unauthorized();
        }
    }
}
