using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Models;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// İşlem takibi — audit_logs tablosuna yazar ve okur.
///
/// LogAsync hata fırlatmaz; log yazılamaması asıl işlemi durdurmamalı.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly DatabaseClient _client;
    private readonly SessionContext _session;

    public AuditLogService(DatabaseClient client, SessionContext session)
    {
        _client = client;
        _session = session;
    }

    public async Task LogAsync(string action, string detail)
    {
        // Oturum yoksa log yazılmaz — sessizce geç
        if (!_session.IsLoggedIn) return;

        try
        {
            var log = new AuditLogModel
            {
                UserId = _session.UserId,
                UserName = _session.UserName,
                Role = _session.Role,
                Action = action,
                Detail = detail,
                DeviceName = _session.DeviceName
            };

            await _client.Db.Table<AuditLogModel>().Insert(log);
        }
        catch
        {
            // Log yazma hatası asıl işlemi durdurmamalı — yut
        }
    }

    public async Task<BaseResponse<List<AuditLogModel>>> GetRecentLogsAsync(int limit = 100)
    {
        try
        {
            var result = await _client.Db
                .Table<AuditLogModel>()
                .Select(DatabaseClient.AuditLogColumns)
                .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Descending)
                .Limit(limit)
                .Get();

            return BaseResponse<List<AuditLogModel>>.SuccessResult(result.Models);
        }
        catch (Exception ex)
        {
            return BaseResponse<List<AuditLogModel>>.ErrorResult(
                $"Loglar getirilemedi: {ex.Message}");
        }
    }
}