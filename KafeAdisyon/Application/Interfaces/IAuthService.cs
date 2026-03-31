using KafeAdisyon.Common;

namespace KafeAdisyon.Application.Interfaces;

/// <summary>
/// Supabase Auth ile email/şifre tabanlı kimlik doğrulama sözleşmesi.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Email + şifre ile giriş yapar.
    /// Başarılıysa SessionContext doldurulur; cihaz adı kullanıcıdan alınır.
    /// </summary>
    Task<BaseResponse<string>> LoginAsync(string email, string password, string deviceName);

    /// <summary>
    /// Oturumu kapatır, SessionContext temizlenir.
    /// </summary>
    Task LogoutAsync();
}