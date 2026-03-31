namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Login olan kullanıcının oturum bilgilerini uygulama boyunca taşır.
/// MauiProgram'a AddSingleton olarak kaydedilir.
///
/// JWT token Supabase Auth'dan alınır; diğer servisler bu token'ı
/// DatabaseClient üzerinden Postgrest header'ına enjekte eder.
/// </summary>
public class SessionContext
{
    // ── Kullanıcı Bilgileri ──────────────────────────────────────
    public string UserId { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;   // "admin" | "garson"
    public string DeviceName { get; private set; } = string.Empty;
    public string JwtToken { get; private set; } = string.Empty;

    public bool IsLoggedIn => !string.IsNullOrEmpty(UserId);
    public bool IsAdmin => Role == "admin";

    // ── Oturum Aç ───────────────────────────────────────────────
    public void SetSession(
        string userId,
        string userName,
        string role,
        string deviceName,
        string jwtToken)
    {
        UserId = userId;
        UserName = userName;
        Role = role;
        DeviceName = deviceName;
        JwtToken = jwtToken;
    }

    // ── Oturum Kapat ────────────────────────────────────────────
    public void Clear()
    {
        UserId = string.Empty;
        UserName = string.Empty;
        Role = string.Empty;
        DeviceName = string.Empty;
        JwtToken = string.Empty;
    }
}