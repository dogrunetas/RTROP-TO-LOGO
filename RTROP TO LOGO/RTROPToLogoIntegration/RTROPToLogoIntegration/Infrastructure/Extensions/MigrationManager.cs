using RTROPToLogoIntegration.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Microsoft.AspNetCore.Identity;

namespace RTROPToLogoIntegration.Infrastructure.Extensions
{
    /// <summary>
    /// Veritabanı ve Seed Data yönetim sınıfı.
    /// </summary>
    public static class MigrationManager
    {
        public static IHost MigrateDatabase(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                // AppIdentityDbContext artik Identity klasoru altinda
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<AppIdentityDbContext>();
                    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

                    // Veritabanı yoksa oluştur ve migrationları uygula
                    context.Database.Migrate();
                    
                    // Seed Data
                    if (!userManager.Users.Any())
                    {
                        var adminUser = new IdentityUser
                        {
                            UserName = "Admin", // Kullanıcı adı "Admin" olarak istendi
                            Email = "admin@rtrop.com",
                            EmailConfirmed = true
                        };
                        
                        var result = userManager.CreateAsync(adminUser, "Admin123!").Result;

                        if (result.Succeeded)
                        {
                            Log.Information("Admin kullanıcısı oluşturuldu.");
                        }
                        else
                        {
                            foreach (var error in result.Errors)
                            {
                                Log.Error("Admin oluşturma hatası: {Error}", error.Description);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Veritabanı migration/seed sırasında hata oluştu.");
                }
            }
            return host;
        }
    }
}
