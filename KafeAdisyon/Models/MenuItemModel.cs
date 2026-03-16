using Postgrest.Attributes;
using Postgrest.Models;

namespace KafeAdisyon.Models;

[Table("menu_items")]
public class MenuItemModel : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Column("category")]
    public string Category { get; set; } = string.Empty;
    [Column("price")]
    public double Price { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}