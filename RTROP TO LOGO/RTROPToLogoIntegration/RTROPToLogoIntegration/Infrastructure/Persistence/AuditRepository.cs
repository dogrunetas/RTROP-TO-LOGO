using RTROPToLogoIntegration.Domain.Entities;
using RTROPToLogoIntegration.Infrastructure.Identity;

namespace RTROPToLogoIntegration.Infrastructure.Persistence
{
    public class AuditRepository
    {
        private readonly AppIdentityDbContext _context;

        public AuditRepository(AppIdentityDbContext context)
        {
            _context = context;
        }

        public async Task LogRequestAsync(string transactionId, string endpoint, string method, string requestBody, string clientIp, string? userId)
        {
            var log = new LogIncomingRequest
            {
                TransactionId = transactionId,
                Endpoint = endpoint,
                Method = method,
                RequestBody = requestBody,
                ClientIp = clientIp,
                UserId = userId,
                CreatedAt = DateTime.Now
            };

            await _context.LogIncomingRequests.AddAsync(log);
            await _context.SaveChangesAsync();
        }
    }
}
