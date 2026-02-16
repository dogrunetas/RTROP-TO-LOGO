using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RTROPToLogoIntegration.Infrastructure.Identity
{
    /// <summary>
    /// Kimlik doğrulama işlemleri için DbContext.
    /// Sadece Identity tablolarını yönetir.
    /// </summary>
    public class AppIdentityDbContext : IdentityDbContext
    {
        public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options)
        {
        }
    }
}
