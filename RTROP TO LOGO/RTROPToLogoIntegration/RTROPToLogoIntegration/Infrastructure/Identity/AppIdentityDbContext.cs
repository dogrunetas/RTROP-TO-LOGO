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

        public DbSet<RTROPToLogoIntegration.Domain.Entities.LogIncomingRequest> LogIncomingRequests { get; set; }
        public DbSet<RTROPToLogoIntegration.Domain.Entities.ApplicationLog> Logs { get; set; } // Serilog Logs table

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RTROPToLogoIntegration.Domain.Entities.LogIncomingRequest>()
                .HasIndex(l => l.TransactionId);
        }
    }
}
