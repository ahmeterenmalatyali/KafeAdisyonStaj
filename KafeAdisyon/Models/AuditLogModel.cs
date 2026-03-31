using Postgrest.Attributes;
using Postgrest.Models;

namespace KafeAdisyon.Models;

[Table("audit_logs")]
public class AuditLogModel : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("user_name")]
    public string UserName { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// hesap_kapatma | siparis_iptali | fiyat_guncelleme | urun_ekleme | urun_silme
    /// </summary>
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Column("detail")]
    public string Detail { get; set; } = string.Empty;

    [Column("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}