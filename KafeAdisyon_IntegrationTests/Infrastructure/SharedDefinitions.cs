// ─── Modeller ──────────────────────────────────────────────────────
using Postgrest.Attributes;
using Postgrest.Models;

namespace KafeAdisyon.Models
{
    [Table("menu_items")]
    public class MenuItemModel : BaseModel
    {
        [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
        [Column("name")] public string Name { get; set; } = string.Empty;
        [Column("category")] public string Category { get; set; } = string.Empty;
        [Column("price")] public double Price { get; set; }
        [Column("is_active")] public bool IsActive { get; set; } = true;
        [Column("created_at")] public DateTime CreatedAt { get; set; }
    }

    [Table("orders")]
    public class OrderModel : BaseModel
    {
        [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
        [Column("table_id")] public string TableId { get; set; } = string.Empty;
        [Column("status")] public string Status { get; set; } = "aktif";
        [Column("total")] public double Total { get; set; }
        [Column("created_at")] public DateTime CreatedAt { get; set; }
    }

    [Table("order_items")]
    public class OrderItemModel : BaseModel
    {
        [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
        [Column("order_id")] public string OrderId { get; set; } = string.Empty;
        [Column("menu_item_id")] public string MenuItemId { get; set; } = string.Empty;
        [Column("quantity")] public int Quantity { get; set; } = 1;
        [Column("price")] public double Price { get; set; }
        [Column("created_at")] public DateTime CreatedAt { get; set; }
    }

    [Table("tables")]
    public class TableModel : BaseModel
    {
        [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
        [Column("name")] public string Name { get; set; } = string.Empty;
        [Column("status")] public string Status { get; set; } = "bos";
        [Column("created_at")] public DateTime CreatedAt { get; set; }
    }
}

// ─── Common ────────────────────────────────────────────────────────
namespace KafeAdisyon.Common
{
    public class BaseResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static BaseResponse<T> SuccessResult(T? data, string message = "")
            => new() { Success = true, Data = data, Message = message };

        public static BaseResponse<T> ErrorResult(string message)
            => new() { Success = false, Message = message };
    }
}

// ─── DTOs ──────────────────────────────────────────────────────────
namespace KafeAdisyon.Application.DTOs.RequestModels
{
    public class AddMenuItemRequest { public string Name { get; set; } = ""; public string Category { get; set; } = ""; public double Price { get; set; } }
    public class UpdateMenuItemRequest { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string Category { get; set; } = ""; public double Price { get; set; } public bool IsActive { get; set; } = true; }
    public class UpdateTableStatusRequest { public string TableId { get; set; } = ""; public string Status { get; set; } = ""; }
    public class CloseOrderRequest { public string OrderId { get; set; } = ""; public string TableId { get; set; } = ""; public double FinalTotal { get; set; } }
    public class AddOrderItemRequest { public string OrderId { get; set; } = ""; public string MenuItemId { get; set; } = ""; public int Quantity { get; set; } = 1; public double Price { get; set; } }
    public class UpdateOrderItemQuantityRequest { public string ItemId { get; set; } = ""; public int Quantity { get; set; } }
}

// ─── Interfaces ────────────────────────────────────────────────────
namespace KafeAdisyon.Application.Interfaces
{
    using KafeAdisyon.Application.DTOs.RequestModels;
    using KafeAdisyon.Common;
    using KafeAdisyon.Models;

    public interface IMenuService
    {
        Task<BaseResponse<List<MenuItemModel>>> GetAllMenuItemsAsync();
        Task<BaseResponse<MenuItemModel>> AddMenuItemAsync(AddMenuItemRequest request);
        Task<BaseResponse<object>> UpdateMenuItemAsync(UpdateMenuItemRequest request);
        Task<BaseResponse<object>> DeleteMenuItemAsync(string id);
    }

    public interface ITableService
    {
        Task<BaseResponse<List<TableModel>>> GetAllTablesAsync();
        Task<BaseResponse<object>> UpdateTableStatusAsync(UpdateTableStatusRequest request);
    }

    public interface IOrderService
    {
        Task<BaseResponse<OrderModel?>> GetActiveOrderByTableAsync(string tableId);
        Task<BaseResponse<OrderModel>> CreateOrderAsync(string tableId);
        Task<BaseResponse<object>> CloseOrderAsync(CloseOrderRequest request);
        Task<BaseResponse<List<OrderItemModel>>> GetOrderItemsAsync(string orderId);
        Task<BaseResponse<OrderItemModel>> AddOrderItemAsync(AddOrderItemRequest request);
        Task<BaseResponse<object>> UpdateOrderItemQuantityAsync(UpdateOrderItemQuantityRequest request);
        Task<BaseResponse<object>> RemoveOrderItemAsync(string itemId);
    }
}
