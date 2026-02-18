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
                bool shouldLogBody = context.Request.Method == HttpMethods.Post || context.Request.Method == HttpMethods.Put;

                if (shouldLogBody)
                {
                    // Body Okuma Hazırlığı (Girişte)
                    context.Request.EnableBuffering();
                }

                try
                {
                    // 4. SONRAKİ MIDDLEWARE'E GEÇ (Controller çalışsın, Auth olsun)
                    await _next(context);
                }
                finally
                {
                    // 5. İŞLEM SONRASI LOGLAMA (Post-Execution or On Exception)
                    // Finally ensures logging happens even if _next throws an exception (Blind Spot Fix)
                    if (shouldLogBody)
                    {
                        await LogRequestSafeAsync(context, auditRepository, transactionId);
                    }
                }
            }
        }

        private async Task LogRequestSafeAsync(HttpContext context, AuditRepository auditRepository, string transactionId)
        {
            try
            {
                var endpoint = context.Request.Path.ToString();
                string body = "";

                // Sensitive Data Exposure Check
                if (endpoint.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                    endpoint.Contains("auth", StringComparison.OrdinalIgnoreCase))
                {
                    body = "[SENSITIVE DATA HIDDEN]";
                }
                else
                {
                    // Body Stream'i başa sarıp oku
                    if (context.Request.Body.CanSeek)
                    {
                        context.Request.Body.Position = 0;
                        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                        {
                            body = await reader.ReadToEndAsync();
                            // Stream'i tekrar başa sar (Her ihtimale karşı)
                            context.Request.Body.Position = 0;
                        }
                    }
                }

                // UserId Yakala (Auth middleware çalıştıysa doludur)
                var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var method = context.Request.Method;
                var clientIp = context.Connection.RemoteIpAddress?.ToString();

                await auditRepository.LogRequestAsync(transactionId, endpoint, method, body, clientIp, userId);
            }
            catch (Exception ex)
            {
                // Failsafe: Audit failure should not crash the user request
                Console.WriteLine($"Audit Log Hatası: {ex.Message}");
            }
        }
    }
}
