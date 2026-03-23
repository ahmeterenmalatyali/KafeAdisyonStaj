using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Offline;
using KafeAdisyon.Models;
using Microsoft.Extensions.Logging;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Decorator pattern: IOrderService arayüzünü uygular, içinde gerçek OrderService'i sarar.
///
/// Bağlantı VAR  → isteği doğrudan OrderService'e iletir (mevcut davranış).
/// Bağlantı YOK  → işlemi OfflineQueue'ya kaydeder; UI'yı yanıltmamak için
///                 başarılı gibi görünen bir yanıt döner (optimistic).
///
/// ConnectivityChanged eventi tetiklendiğinde FlushQueueAsync() çağrılır;
/// kuyrukta bekleyen tüm işlemler Supabase'e sırayla gönderilir.
/// </summary>
public class OfflineAwareOrderService : IOrderService
{
    private readonly IOrderService _inner;          // asıl OrderService
    private readonly IConnectivityService _conn;
    private readonly OfflineQueue _queue;
    private readonly ILogger<OfflineAwareOrderService> _logger;
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    // ─── İşlem adı sabitleri (kuyruk öğeleriyle eşleşmeli) ──────────────────
    private const string OpCreateOrder = "CreateOrder";
    private const string OpCloseOrder = "CloseOrder";
    private const string OpAddItem = "AddItem";
    private const string OpUpdateQuantity = "UpdateQuantity";
    private const string OpRemoveItem = "RemoveItem";
    private const string OpUpdateTableStatus = "UpdateTableStatus";

    public OfflineAwareOrderService(
        OrderService inner,               // somut tip — DI'dan gelen asıl servis
        IConnectivityService connectivity,
        OfflineQueue queue,
        ILogger<OfflineAwareOrderService> logger)
    {
        _inner = inner;
        _conn = connectivity;
        _queue = queue;
        _logger = logger;

        // Bağlantı gelince kuyruğu otomatik boşalt
        _conn.ConnectivityChanged += async (_, isConnected) =>
        {
            if (isConnected)
            {
                _logger.LogInformation("Bağlantı yeniden kuruldu — offline kuyruk boşaltılıyor.");
                await FlushQueueAsync();
            }
        };
    }

    // ─── IOrderService implementasyonu ───────────────────────────────────────

    public Task<BaseResponse<OrderModel?>> GetActiveOrderByTableAsync(string tableId)
        => _inner.GetActiveOrderByTableAsync(tableId);   // okuma — her zaman DB'den

    public Task<BaseResponse<List<OrderItemModel>>> GetOrderItemsAsync(string orderId)
        => _inner.GetOrderItemsAsync(orderId);            // okuma — her zaman DB'den

    public async Task<BaseResponse<OrderModel>> CreateOrderAsync(string tableId)
    {
        if (_conn.IsConnected)
            return await _inner.CreateOrderAsync(tableId);

        _logger.LogWarning("Offline: CreateOrder kuyruğa alındı. TableId={TableId}", tableId);
        await _queue.EnqueueAsync(OpCreateOrder, tableId);

        // Geçici model döndür — UI bunu optimistic olarak kullanır
        return BaseResponse<OrderModel>.SuccessResult(new OrderModel
        {
            Id = $"_offline_{Guid.NewGuid()}",
            TableId = tableId,
            Status = "aktif",
            Total = 0
        }, "[Offline] Sipariş geçici olarak oluşturuldu");
    }

    public async Task<BaseResponse<object>> CloseOrderAsync(CloseOrderRequest request)
    {
        if (_conn.IsConnected)
            return await _inner.CloseOrderAsync(request);

        _logger.LogWarning("Offline: CloseOrder kuyruğa alındı. OrderId={Id}", request.OrderId);
        await _queue.EnqueueAsync(OpCloseOrder, request);
        return BaseResponse<object>.SuccessResult(null, "[Offline] Hesap kapatma kuyruğa alındı");
    }

    public async Task<BaseResponse<OrderItemModel>> AddOrderItemAsync(AddOrderItemRequest request)
    {
        if (_conn.IsConnected)
            return await _inner.AddOrderItemAsync(request);

        _logger.LogWarning("Offline: AddItem kuyruğa alındı. MenuItemId={Id}", request.MenuItemId);
        await _queue.EnqueueAsync(OpAddItem, request);

        return BaseResponse<OrderItemModel>.SuccessResult(new OrderItemModel
        {
            Id = $"_offline_{Guid.NewGuid()}",
            OrderId = request.OrderId,
            MenuItemId = request.MenuItemId,
            Quantity = request.Quantity,
            Price = request.Price
        }, "[Offline] Ürün geçici olarak eklendi");
    }

    public async Task<BaseResponse<object>> UpdateOrderItemQuantityAsync(
        UpdateOrderItemQuantityRequest request)
    {
        if (_conn.IsConnected)
            return await _inner.UpdateOrderItemQuantityAsync(request);

        _logger.LogWarning("Offline: UpdateQuantity kuyruğa alındı. ItemId={Id}", request.ItemId);
        await _queue.EnqueueAsync(OpUpdateQuantity, request);
        return BaseResponse<object>.SuccessResult(null, "[Offline] Miktar güncellemesi kuyruğa alındı");
    }

    public async Task<BaseResponse<object>> RemoveOrderItemAsync(string itemId)
    {
        if (_conn.IsConnected)
            return await _inner.RemoveOrderItemAsync(itemId);

        _logger.LogWarning("Offline: RemoveItem kuyruğa alındı. ItemId={Id}", itemId);
        await _queue.EnqueueAsync(OpRemoveItem, itemId);
        return BaseResponse<object>.SuccessResult(null, "[Offline] Kaldırma işlemi kuyruğa alındı");
    }
    public async Task FlushQueueAsync()
    {
        if (!await _flushLock.WaitAsync(0))
        {
            _logger.LogDebug("FlushQueue zaten çalışıyor, atlanıyor.");
            return;
        }

        try
        {
            var items = await _queue.GetAllAsync();
            if (items.Count == 0) return;

            _logger.LogInformation("Offline kuyruk boşaltılıyor: {Count} işlem", items.Count);

            foreach (var item in items)
            {
                if (!_conn.IsConnected)
                {
                    _logger.LogWarning("Flush sırasında bağlantı kesildi, duruluyor.");
                    return;
                }

                try
                {
                    var success = await ExecuteQueueItemAsync(item);
                    if (success)
                        await _queue.RemoveAsync(item.Id);
                    else
                        await _queue.IncrementRetryAsync(item.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kuyruk öğesi işlenirken hata. Id={Id} Op={Op}",
                        item.Id, item.Operation);
                    await _queue.IncrementRetryAsync(item.Id);
                }
            }

            _logger.LogInformation("Offline kuyruk boşaltma tamamlandı.");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task<bool> ExecuteQueueItemAsync(OfflineQueue.QueueItem item)
    {
        _logger.LogDebug("Kuyruk öğesi işleniyor: Op={Op} Id={Id}", item.Operation, item.Id);

        switch (item.Operation)
        {
            case OpCreateOrder:
                {
                    var tableId = OfflineQueue.Deserialize<string>(item.Payload);
                    var r = await _inner.CreateOrderAsync(tableId);
                    return r.Success;
                }
            case OpCloseOrder:
                {
                    var req = OfflineQueue.Deserialize<CloseOrderRequest>(item.Payload);
                    var r = await _inner.CloseOrderAsync(req);
                    return r.Success;
                }
            case OpAddItem:
                {
                    var req = OfflineQueue.Deserialize<AddOrderItemRequest>(item.Payload);
                    var r = await _inner.AddOrderItemAsync(req);
                    return r.Success;
                }
            case OpUpdateQuantity:
                {
                    var req = OfflineQueue.Deserialize<UpdateOrderItemQuantityRequest>(item.Payload);
                    var r = await _inner.UpdateOrderItemQuantityAsync(req);
                    return r.Success;
                }
            case OpRemoveItem:
                {
                    var itemId = OfflineQueue.Deserialize<string>(item.Payload);
                    var r = await _inner.RemoveOrderItemAsync(itemId);
                    return r.Success;
                }
            default:
                _logger.LogWarning("Bilinmeyen kuyruk işlemi: {Op}", item.Operation);
                return true; // kuyruğu bloke etmesin, sil
        }
    }
}