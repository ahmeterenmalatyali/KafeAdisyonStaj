// ─────────────────────────────────────────────────────────────────
// Modeller, interface'ler ve BaseResponse — ana projeden kopyalandı
// Test projesi ana projeye referans vermiyor, bağımsız çalışır.
// ─────────────────────────────────────────────────────────────────

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

namespace KafeAdisyon.Models
{
    public class MenuItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Price { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class OrderModel
    {
        public string Id { get; set; } = string.Empty;
        public string TableId { get; set; } = string.Empty;
        public string Status { get; set; } = "aktif";
        public double Total { get; set; }
    }

    public class OrderItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string MenuItemId { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public double Price { get; set; }
    }

    public class TableModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "bos";
    }
}

namespace KafeAdisyon.Application.DTOs.RequestModels
{
    public class AddMenuItemRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Price { get; set; }
    }

    public class UpdateMenuItemRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Price { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateTableStatusRequest
    {
        public string TableId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class CloseOrderRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string TableId { get; set; } = string.Empty;
        public double FinalTotal { get; set; }
    }

    public class AddOrderItemRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string MenuItemId { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public double Price { get; set; }
    }

    public class UpdateOrderItemQuantityRequest
    {
        public string ItemId { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}

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
