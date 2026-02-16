using System.Net;
using Serilog.Context;

namespace RTROPToLogoIntegration.Middlewares
{
    /// <summary>
    /// Gelen isteklerin IP adresini ve kaynağını (Yerel/Dış) analiz eder ve Serilog Context'e ekler.
    /// </summary>
    public class IpLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IpLoggingMiddleware> _logger;

        public IpLoggingMiddleware(RequestDelegate next, ILogger<IpLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            // IP Adresini al (X-Forwarded-For varsa onu, yoksa RemoteIpAddress)
            // Not: Program.cs tarafında ForwardedHeadersOptions yapılandırılmış olmalı.
            var remoteIp = context.Connection.RemoteIpAddress;
           
            string clientIp = remoteIp?.ToString() ?? "Unknown";

            // Kaynak analizi (Local vs External)
            string requestSource = IsLocal(remoteIp) ? "Local Network" : "External (Production)";

            // Serilog Context'e ekle (Bu scope içindeki tüm loglarda görünecek)
            using (LogContext.PushProperty("ClientIp", clientIp))
            using (LogContext.PushProperty("RequestSource", requestSource))
            {
                await _next(context);
            }
        }

        /// <summary>
        /// IP adresinin yerel ağdan mı yoksa dış dünyadan mı geldiğini kontrol eder.
        /// </summary>
        /// <param name="ipAddress">Kontrol edilecek IP adresi</param>
        /// <returns>Yerel ise true, değilse false</returns>
        private bool IsLocal(IPAddress ipAddress)
        {
            if (ipAddress == null) return false;

            // Loopback (::1, 127.0.0.1) kontrolü
            if (IPAddress.IsLoopback(ipAddress)) return true;

            // IPv4 ise private IP aralıklarını kontrol et
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                byte[] bytes = ipAddress.GetAddressBytes();
                // 10.x.x.x
                if (bytes[0] == 10) return true;
                // 172.16.x.x - 172.31.x.x
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                // 192.168.x.x
                if (bytes[0] == 192 && bytes[1] == 168) return true;
            }

            // IPv6 için implementasyon eklenebilir, şimdilik sadece Loopback kontrol ediliyor.
            // Production ortamında ::1 harici IPv6 kullanımı nadir olabilir veya özel yapılandırma gerekebilir.

            return false;
        }
    }
}
