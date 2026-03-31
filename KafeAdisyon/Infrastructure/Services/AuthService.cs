using System.Net.Http.Json;
using System.Text.Json;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Models;
using Microsoft.Extensions.Configuration;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Supabase Auth REST API ile email/şifre girişi.
///
/// AGENTS.md notu: Supabase SDK InitializeAsync deadlock yapıyor.
/// Bu yüzden SDK yerine HttpClient + doğrudan REST endpoint kullanılıyor.
///
/// Endpoint: POST {url}/auth/v1/token?grant_type=password
/// </summary>
public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly DatabaseClient _db;
    private readonly SessionContext _session;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;

    public AuthService(
        IConfiguration config,
        DatabaseClient db,
        SessionContext session)
    {
        _db = db;
        _session = session;
        _supabaseUrl = config["Supabase:Url"]!.TrimEnd('/');
        _supabaseKey = config["Supabase:PublishableKey"]!;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", _supabaseKey);
    }

    public async Task<BaseResponse<string>> LoginAsync(
        string email, string password, string deviceName)
    {
        try
        {
            // 1) Supabase Auth — token al
            var url = $"{_supabaseUrl}/auth/v1/token?grant_type=password";

            var payload = new { email, password };
            var response = await _http.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                // Supabase hata mesajı JSON içinde "error_description" alanında geliyor
                var errDoc = JsonDocument.Parse(err);
                var msg = errDoc.RootElement
                    .TryGetProperty("error_description", out var prop)
                    ? prop.GetString() ?? "Giriş başarısız."
                    : "Giriş başarısız.";
                return BaseResponse<string>.ErrorResult(msg);
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var token = root.GetProperty("access_token").GetString()!;
            var userId = root.GetProperty("user")
                             .GetProperty("id").GetString()!;

            // 2) profiles tablosundan kullanıcı adı + rol al
            // JWT'yi Postgrest header'ına enjekte et
            _db.SetAuthToken(token);

            var profileResult = await _db.Db
                .Table<ProfileModel>()
                .Where(p => p.Id == userId)
                .Get();

            var profile = profileResult.Models.FirstOrDefault();
            if (profile == null)
                return BaseResponse<string>.ErrorResult(
                    "Profil bulunamadı. Supabase'de profiles tablosuna kayıt ekleyin.");

            // 3) SessionContext'e yaz
            _session.SetSession(
                userId: userId,
                userName: profile.FullName,
                role: profile.Role,
                deviceName: deviceName.Trim(),
                jwtToken: token);

            return BaseResponse<string>.SuccessResult(
                profile.Role, $"Hoş geldiniz, {profile.FullName}!");
        }
        catch (Exception ex)
        {
            return BaseResponse<string>.ErrorResult($"Giriş hatası: {ex.Message}");
        }
    }

    public Task LogoutAsync()
    {
        _session.Clear();
        _db.ClearAuthToken();
        return Task.CompletedTask;
    }
}