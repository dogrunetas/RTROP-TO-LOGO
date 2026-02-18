using Serilog.Context;
using RTROPToLogoIntegration.Infrastructure.Persistence;
using System.Security.Claims;
using System.Text;

namespace RTROPToLogoIntegration.Infrastructure.Middleware
{
    public class RequestAuditMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestAuditMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, AuditRepository auditRepository)
        {
            // 1. ULID Üret
            var transactionId = Ulid.NewUlid().ToString();

            // 2. Response Header'a Ekle
            context.Response.Headers.Append("X-Transaction-ID", transactionId);

            // 3. SERILOG CONTEXT (Bu blok içindeki tüm loglara ID otomatik eklenir)
            using (LogContext.PushProperty("TransactionId", transactionId))
            {
                // Sadece POST ve PUT isteklerini logla
                if (context.Request.Method == HttpMethods.Post || context.Request.Method == HttpMethods.Put)
                {
                    // Body Okuma Hazırlığı (Girişte)
                    context.Request.EnableBuffering();
                }

                // 4. SONRAKİ MIDDLEWARE'E GEÇ (Controller çalışsın, Auth olsun)
                await _next(context);

                // 5. İŞLEM SONRASI LOGLAMA (Post-Execution)
                if (context.Request.Method == HttpMethods.Post || context.Request.Method == HttpMethods.Put)
                {
                    try
                    {
                        // Body Stream'i başa sarıp oku
                        context.Request.Body.Position = 0;
                        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                        {
                            var body = await reader.ReadToEndAsync();
                            
                            // Stream'i tekrar başa sar (Her ihtimale karşı)
                            context.Request.Body.Position = 0;

                            // UserId Yakala (Auth middleware çalıştıysa doludur)
                            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            
                            var endpoint = context.Request.Path;
                            var method = context.Request.Method;
                            var clientIp = context.Connection.RemoteIpAddress?.ToString();

                            await auditRepository.LogRequestAsync(transactionId, endpoint, method, body, clientIp, userId);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Audit Log Hatası: {ex.Message}");
                    }
                }
            }
        }
    }
}
