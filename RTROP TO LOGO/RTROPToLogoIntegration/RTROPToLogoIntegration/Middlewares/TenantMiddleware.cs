using Serilog.Context;

namespace RTROPToLogoIntegration.Middlewares
{
    /// <summary>
    /// İstek başlıklarında firma numarasını (x-firm-no) kontrol eder.
    /// </summary>
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            // Sadece /api ile başlayan ancak /api/auth olmayan istekleri kontrol et
            // Swagger (/swagger) ve diğer statik dosyalar bu kontrolden geçmemeli
            if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/api/auth"))
            {
                await _next(context);
                return;
            }

            if (context.Request.Headers.TryGetValue("x-firm-no", out var firmNo) && !string.IsNullOrWhiteSpace(firmNo))
            {
                // Log Context'e ekle
                using (LogContext.PushProperty("FirmNo", firmNo.ToString()))
                {
                    await _next(context);
                }
            }
            else
            {
                // Header eksikse hata dön ve logla
                // IP adresi IpLoggingMiddleware sayesinde zaten Context'te var.
                Serilog.Log.Warning("x-firm-no başlığı eksik. Erişim reddedildi.");
                context.Response.StatusCode = 400; // Bad Request
                await context.Response.WriteAsync("x-firm-no header is required.");
            }
        }
    }
}
