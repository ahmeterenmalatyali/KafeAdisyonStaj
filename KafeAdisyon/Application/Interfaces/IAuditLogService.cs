using KafeAdisyon.Common;
using KafeAdisyon.Models;

namespace KafeAdisyon.Application.Interfaces;

/// <summary>
/// İşlem takibi — log yazma ve okuma sözleşmesi.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Bir işlemi audit_logs tablosuna kaydeder.
    /// Hata olursa sessizce geçer — log yazılamaması asıl işlemi durdurmamalı.
    /// </summary>
    Task LogAsync(string action, string detail);

    /// <summary>
    /// Son N kaydı getirir (Admin paneli için).
    /// </summary>
    Task<BaseResponse<List<AuditLogModel>>> GetRecentLogsAsync(int limit = 100);
}