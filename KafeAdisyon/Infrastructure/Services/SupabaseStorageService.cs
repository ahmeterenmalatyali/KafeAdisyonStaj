using KafeAdisyon.Common;
using Microsoft.Extensions.Configuration;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Supabase Storage REST API üzerinden dosya yükleme servisi.
///
/// AGENTS.md notu: Supabase.Client.InitializeAsync() deadlock yapıyor.
/// Bu yüzden storage işlemlerinde de SDK kullanmıyoruz —
/// HttpClient ile doğrudan REST API çağrısı yapıyoruz.
///
/// Fabrikadaki MinIO mantığıyla birebir aynı:
///   MinIO  → mc alias + mc cp
///   Burada → HTTP PUT /storage/v1/object/{bucket}/{path}
///
/// KURULUM GEREKSİNİMİ:
///   Supabase Dashboard → Storage → New bucket → "reports" (Public: true)
/// </summary>
public class SupabaseStorageService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    private const string Bucket = "reports";

    public SupabaseStorageService(IConfiguration config)
    {
        var url = config["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url bulunamadı.");
        _apiKey = config["Supabase:PublishableKey"]
            ?? throw new InvalidOperationException("Supabase:PublishableKey bulunamadı.");

        _baseUrl = url.TrimEnd('/') + "/storage/v1";

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", _apiKey);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    /// <summary>
    /// PDF byte dizisini Supabase Storage'a yükler, public URL döndürür.
    /// </summary>
    /// <param name="fileName">Depolama yolu — örn. "2026-03-27_gunluk.pdf"</param>
    /// <param name="pdfBytes">QuestPDF'den gelen byte[]</param>
    public async Task<BaseResponse<string>> UploadPdfAsync(string fileName, byte[] pdfBytes)
    {
        try
        {
            var uploadUrl = $"{_baseUrl}/object/{Bucket}/{fileName}";

            using var content = new ByteArrayContent(pdfBytes);
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

            // Aynı isimde dosya varsa üzerine yaz
            var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = content
            };
            request.Headers.Add("x-upsert", "true");

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return BaseResponse<string>.ErrorResult(
                    $"Storage yükleme başarısız ({(int)response.StatusCode}): {body}");
            }

            // Public download URL — bucket "Public" olarak işaretlenmeli
            var publicUrl = $"{_baseUrl}/object/public/{Bucket}/{fileName}";
            return BaseResponse<string>.SuccessResult(publicUrl, "PDF buluta yüklendi.");
        }
        catch (Exception ex)
        {
            return BaseResponse<string>.ErrorResult($"Storage hatası: {ex.Message}");
        }
    }
}