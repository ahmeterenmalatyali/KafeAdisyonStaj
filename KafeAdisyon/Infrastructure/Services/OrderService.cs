using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Models;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Sipariş ve sipariş kalemi işlemlerinin implementasyonu.
/// Hesap kapatma ve sipariş iptali IAuditLogService üzerinden loglanır.
/// </summary>
public class OrderService : IOrderService
{
    private readonly DatabaseClient _client;
    private readonly ITableService _tableService;
    private readonly IAuditLogService _audit;

    public OrderService(
        DatabaseClient client,
        ITableService tableService,
        IAuditLogService audit)
    {
        _client = client;
        _tableService = tableService;
        _audit = audit;
    }

    public async Task<BaseResponse<OrderModel?>> GetActiveOrderByTableAsync(string tableId)
    {
        try
        {
            var result = await _client.Db
                .Table<OrderModel>()
                .Select(DatabaseClient.OrderColumns)
                .Where(o => o.Status == "aktif" && o.TableId == tableId)
                .Get();

            return BaseResponse<OrderModel?>.SuccessResult(result.Models.FirstOrDefault());
        }
        catch (Exception ex)
        {
            return BaseResponse<OrderModel?>.ErrorResult(
                $"Aktif sipariş getirilemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<OrderModel>> CreateOrderAsync(string tableId)
    {
        try
        {
            var order = new OrderModel { TableId = tableId, Status = "aktif", Total = 0 };
            var result = await _client.Db.Table<OrderModel>().Insert(order);
            var created = result.Models.First();

            return BaseResponse<OrderModel>.SuccessResult(created, "Sipariş oluşturuldu");
        }
        catch (Exception ex)
        {
            return BaseResponse<OrderModel>.ErrorResult($"Sipariş oluşturulamadı: {ex.Message}");
        }
    }

    public async Task<BaseResponse<object>> CloseOrderAsync(CloseOrderRequest request)
    {
        try
        {
            await _client.Db
                .Table<OrderModel>()
                .Where(o => o.Id == request.OrderId)
                .Set(o => o.Status, "odendi")
                .Set(o => o.Total, request.FinalTotal)
                .Update();

            var tableResult = await _tableService.UpdateTableStatusAsync(
                new UpdateTableStatusRequest
                {
                    TableId = request.TableId,
                    Status = "bos"
                });

            if (!tableResult.Success)
                return BaseResponse<object>.ErrorResult(tableResult.Message);

            // ── Audit Log ──────────────────────────────────────────
            await _audit.LogAsync(
                action: "hesap_kapatma",
                detail: $"Masa {request.TableId} — ₺{request.FinalTotal:F2}");

            return BaseResponse<object>.SuccessResult(null, "Hesap kapatıldı");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Hesap kapatılamadı: {ex.Message}");
        }
    }

    public async Task<BaseResponse<object>> CancelOrderAsync(string orderId, string tableId)
    {
        try
        {
            // Sipariş kalemlerini sil
            var items = await _client.Db
                .Table<OrderItemModel>()
                .Where(i => i.OrderId == orderId)
                .Get();

            foreach (var item in items.Models)
            {
                await _client.Db
                    .Table<OrderItemModel>()
                    .Where(i => i.Id == item.Id)
                    .Delete();
            }

            // Siparişi iptal et
            await _client.Db
                .Table<OrderModel>()
                .Where(o => o.Id == orderId)
                .Set(o => o.Status, "iptal")
                .Update();

            // Masayı boşalt
            await _tableService.UpdateTableStatusAsync(
                new UpdateTableStatusRequest { TableId = tableId, Status = "bos" });

            // ── Audit Log ──────────────────────────────────────────
            await _audit.LogAsync(
                action: "siparis_iptali",
                detail: $"Sipariş {orderId} iptal edildi (Masa {tableId})");

            return BaseResponse<object>.SuccessResult(null, "Sipariş iptal edildi");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Sipariş iptal edilemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<List<OrderItemModel>>> GetOrderItemsAsync(string orderId)
    {
        try
        {
            var result = await _client.Db
                .Table<OrderItemModel>()
                .Select(DatabaseClient.OrderItemColumns)
                .Where(i => i.OrderId == orderId)
                .Get();

            return BaseResponse<List<OrderItemModel>>.SuccessResult(result.Models);
        }
        catch (Exception ex)
        {
            return BaseResponse<List<OrderItemModel>>.ErrorResult(
                $"Sipariş kalemleri getirilemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<OrderItemModel>> AddOrderItemAsync(AddOrderItemRequest request)
    {
        try
        {
            var item = new OrderItemModel
            {
                OrderId = request.OrderId,
                MenuItemId = request.MenuItemId,
                Quantity = request.Quantity,
                Price = request.Price
            };

            var result = await _client.Db.Table<OrderItemModel>().Insert(item);
            var inserted = result.Models.First();

            return BaseResponse<OrderItemModel>.SuccessResult(inserted, "Ürün siparişe eklendi");
        }
        catch (Exception ex)
        {
            return BaseResponse<OrderItemModel>.ErrorResult($"Ürün siparişe eklenemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<object>> UpdateOrderItemQuantityAsync(
        UpdateOrderItemQuantityRequest request)
    {
        try
        {
            await _client.Db
                .Table<OrderItemModel>()
                .Where(i => i.Id == request.ItemId)
                .Set(i => i.Quantity, request.Quantity)
                .Update();

            return BaseResponse<object>.SuccessResult(null, "Miktar güncellendi");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Miktar güncellenemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<object>> RemoveOrderItemAsync(string itemId)
    {
        try
        {
            await _client.Db
                .Table<OrderItemModel>()
                .Where(i => i.Id == itemId)
                .Delete();

            return BaseResponse<object>.SuccessResult(null, "Ürün sepetten kaldırıldı");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Ürün kaldırılamadı: {ex.Message}");
        }
    }
}