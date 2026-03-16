using Postgrest.Attributes;
using Postgrest.Models;

namespace KafeAdisyon.Models;

[Table("tables")]
public class TableModel : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "bos";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}