using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Common;
using KafeAdisyon.Models;

namespace KafeAdisyon.Application.Interfaces;

/// <summary>
/// Sipariş ve sipariş kalemi işlemleri için servis sözleşmesi.
/// </summary>
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
