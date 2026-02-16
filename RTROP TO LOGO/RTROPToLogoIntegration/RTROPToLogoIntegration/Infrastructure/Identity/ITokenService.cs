using Microsoft.AspNetCore.Identity;
using RTROPToLogoIntegration.Domain.Models;

namespace RTROPToLogoIntegration.Infrastructure.Identity
{
    public interface ITokenService
    {
        Task<AuthResponse> GenerateTokenAsync(IdentityUser user);
    }
}
