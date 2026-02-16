using System.Text;
using System.Text.Json;
using RTROPToLogoIntegration.Domain.Models;
using Serilog;

namespace RTROPToLogoIntegration.Infrastructure.Services
{
    /// <summary>
    /// Logo REST Servisleri ile iletişim kuran servis.
    /// </summary>
    public class LogoClientService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        // Basit bir in-memory cache
        private LogoToken _cachedToken;
        private DateTime _tokenExpiry;

        public LogoClientService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
            // Appsettings'den yeni eklenen LogoRestSettings bölümünü oku
            var baseUrl = _configuration["LogoRestSettings:BaseUrl"] ?? "http://localhost:32001/api/v1/"; 
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        /// <summary>
        /// Logo REST Servisi'nden Token alır.
        /// </summary>
        private async Task<string> GetTokenAsync()
        {
            if (_cachedToken != null && DateTime.Now < _tokenExpiry)
            {
                return _cachedToken.AccessToken;
            }

            var username = _configuration["LogoRestSettings:Username"];
            var password = _configuration["LogoRestSettings:Password"];
            var firmNo = _configuration["LogoRestSettings:FirmNo"];
            var apiKey = _configuration["LogoRestSettings:ApiKey"];

            // DEBUG LOGLARI
            System.Console.WriteLine("----------------------------------------------------------------");
            System.Console.WriteLine($"DEBUG: Logo Token İsteği Hazırlanıyor...");
            System.Console.WriteLine($"DEBUG: BaseAddress: {_httpClient.BaseAddress}");
            System.Console.WriteLine($"DEBUG: Endpoint: token");
            System.Console.WriteLine($"DEBUG: Username: {username}");
            System.Console.WriteLine($"DEBUG: Password: {password}");
            System.Console.WriteLine($"DEBUG: FirmNo: {firmNo}");
            System.Console.WriteLine($"DEBUG: ApiKey (Raw): {apiKey}");
            System.Console.WriteLine("----------------------------------------------------------------");

            // 1. Authorization Header'ı ayarla (Basic Auth - ApiKey)
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            if (!string.IsNullOrEmpty(apiKey))
            {
                // ApiKey zaten "Basic ..." formatında geliyor, direkt ekliyoruz.
                // Eğer sadece key gelirse "Basic " + apiKey yapılmalı. 
                // İstenen formatta ApiKey "Basic RE9..." şeklinde verildiği için direkt parse ediyoruz.
                // Ancak HttpClient Authorization header'ı AuthenticationHeaderValue bekler.
                // Parse işlemi:
                var authHeaderParts = apiKey.Split(' ');
                if (authHeaderParts.Length == 2)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(authHeaderParts[0], authHeaderParts[1]);
                }
                else 
                {
                     // Fallback veya direkt string ekleme (DefaultRequestHeaders.TryAddWithoutValidation)
                     _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
                }
            }

            // 2. Body oluştur (text/plain)
            // Postman örneğine uygun şekilde raw string olarak gönderiyoruz.
            var body = $"grant_type=password&username={username}&firmno={firmNo}&password={password}";
            var content = new StringContent(body, Encoding.UTF8, "text/plain");

            // DEBUG: Body ve Header Kontrolü
            System.Console.WriteLine($"DEBUG: REQUEST BODY (Text/Plain): {body}");
            if (_httpClient.DefaultRequestHeaders.Authorization != null)
            {
                System.Console.WriteLine($"DEBUG: REQUEST AUTH HEADER: {_httpClient.DefaultRequestHeaders.Authorization.ToString()}");
            }
            else
            {
                 System.Console.WriteLine($"DEBUG: REQUEST AUTH HEADER: NULL!");
            }
            System.Console.WriteLine("----------------------------------------------------------------");

            // 3. İsteği gönder
            var response = await _httpClient.PostAsync("token", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(errorContent))
                {
                    errorContent = response.ReasonPhrase ?? "Bilinmeyen Hata";
                }

                Log.Error("Logo Token Alımı Başarısız. Status: {StatusCode}, Error: {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Logo Token Hatası: {response.StatusCode} - {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            _cachedToken = JsonSerializer.Deserialize<LogoToken>(json);
            
            // ExpiresIn genelde saniye cinsindendir.
            _tokenExpiry = DateTime.Now.AddSeconds(_cachedToken.ExpiresIn - 60); // 1 dk güvenlik payı

            return _cachedToken.AccessToken;
        }

        /// <summary>
        /// MRP Taleplerini Logo'ya gönderir.
        /// </summary>
        public async Task PostDemandFicheAsync(LogoMRPModels model, string firmNo)
        {
            try
            {
                var token = await GetTokenAsync();
                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Token alındıktan sonra Authorization header Bearer olarak güncellenmeli.
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                // DEBUG LOGLARI (PostDemandFicheAsync)
                System.Console.WriteLine("----------------------------------------------------------------");
                System.Console.WriteLine($"DEBUG: Logo MRP Kaydı (DemandFiche) Gönderiliyor...");
                System.Console.WriteLine($"DEBUG: BaseAddress: {_httpClient.BaseAddress}");
                System.Console.WriteLine($"DEBUG: Endpoint: demandSlips");
                System.Console.WriteLine($"DEBUG: Token (ilk 10): {token.Substring(0, Math.Min(10, token.Length))}...");
                System.Console.WriteLine($"DEBUG: PAYLOAD (JSON): {json}");
                System.Console.WriteLine("----------------------------------------------------------------");

                // Endpoint: /demandfiches (Örnek)
                var response = await _httpClient.PostAsync("demandSlips", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(error))
                    {
                        error = response.ReasonPhrase ?? "Bilinmeyen Hata";
                    }

                    // Hata durumunda URL'i de logla
                    Log.Error("Logo Post Hatası: {StatusCode} - {Error} - URL: {Url}", response.StatusCode, error, _httpClient.BaseAddress + "demandfiches");
                    throw new Exception($"Logo Servis Hatası: {error}");
                }

                Log.Information("Logo MRP Kaydı Başarılı. Fiş No: {FicheNo}", model.FICHENO);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Logo gönderimi sırasında hata oluştu.");
                throw;
            }
        }
    }
}
