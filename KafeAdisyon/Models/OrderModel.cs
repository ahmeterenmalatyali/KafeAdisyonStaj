using Postgrest.Attributes;
using Postgrest.Models;

namespace KafeAdisyon.Models;

[Table("orders")]
public class OrderModel : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;
    [Column("table_id")]
    public string TableId { get; set; } = string.Empty;
    [Column("status")]
    public string Status { get; set; } = "aktif";
    [Column("total")]
    public double Total { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}