using Postgrest.Attributes;
using Postgrest.Models;

namespace KafeAdisyon.Models;

[Table("order_items")]
public class OrderItemModel : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;
    [Column("order_id")]
    public string OrderId { get; set; } = string.Empty;
    [Column("menu_item_id")]
    public string MenuItemId { get; set; } = string.Empty;
    [Column("quantity")]
    public int Quantity { get; set; } = 1;
    [Column("price")]
    public double Price { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}