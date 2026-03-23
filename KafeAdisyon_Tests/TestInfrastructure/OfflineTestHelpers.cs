// ─────────────────────────────────────────────────────────────────────────────
// Offline test altyapısı — MAUI bağımlılığı içermez, saf .NET 9.
//
// Üç yardımcı:
//   FakeConnectivityService — test kodunun bağlantı durumunu kontrol etmesini sağlar.
//   InMemoryOfflineQueue    — Preferences yerine bellek kullanan kuyruk.
//   TestableOfflineAwareOrderService — OfflineAwareOrderService ile aynı mantık,
//                                      ILogger bağımlılığı olmadan, mock-friendly.
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Models;

namespace KafeAdisyon.Tests.TestInfrastructure
{
    // ─── FakeConnectivityService ──────────────────────────────────────────────

    /// <summary>
    /// Test-kontrollü bağlantı simülatörü.
    /// SetConnected(bool) çağrısı IConnectivityService.ConnectivityChanged eventini tetikler.
    /// </summary>
    public class FakeConnectivityService : IConnectivityService
    {
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public event EventHandler<bool>? ConnectivityChanged;

        public FakeConnectivityService(bool isConnected = true)
        {
            _isConnected = isConnected;
        }

        /// <summary>Bağlantı durumunu değiştirir ve event fırlatır.</summary>
        public void SetConnected(bool value)
        {
            _isConnected = value;
            ConnectivityChanged?.Invoke(this, value);
        }
    }

    // ─── InMemoryOfflineQueue ─────────────────────────────────────────────────

    /// <summary>
    /// MAUI Preferences yerine bellek kullanan kuyruk.
    /// OfflineQueue ile birebir aynı API — testlerde biri yerine diğeri kullanılır.
    /// </summary>
    public class InMemoryOfflineQueue
    {
        private readonly List<QueueItem> _items = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public record QueueItem(
            string Id,
            string Operation,
            string Payload,
            DateTime CreatedAt,
            int RetryCount = 0
        );

        public async Task<List<QueueItem>> GetAllAsync()
        {
            await _lock.WaitAsync();
            try { return new List<QueueItem>(_items); }
            finally { _lock.Release(); }
        }

        public async Task<int> CountAsync()
            => (await GetAllAsync()).Count;

        public async Task EnqueueAsync(string operation, object payload)
        {
            await _lock.WaitAsync();
            try
            {
                _items.Add(new QueueItem(
                    Id: Guid.NewGuid().ToString(),
                    Operation: operation,
                    Payload: JsonSerializer.Serialize(payload),
                    CreatedAt: DateTime.UtcNow
                ));
            }
            finally { _lock.Release(); }
        }

        public async Task RemoveAsync(string itemId)
        {
            await _lock.WaitAsync();
            try { _items.RemoveAll(i => i.Id == itemId); }
            finally { _lock.Release(); }
        }

        public async Task IncrementRetryAsync(string itemId)
        {
            await _lock.WaitAsync();
            try
            {
                var idx = _items.FindIndex(i => i.Id == itemId);
                if (idx >= 0)
                {
                    var old = _items[idx];
                    _items[idx] = old with { RetryCount = old.RetryCount + 1 };
                }
            }
            finally { _lock.Release(); }
        }

        public async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try { _items.Clear(); }
            finally { _lock.Release(); }
        }

        public static T Deserialize<T>(string payload)
            => JsonSerializer.Deserialize<T>(payload)
               ?? throw new InvalidOperationException($"Payload {typeof(T).Name} olarak çözülemedi.");
    }

    // ─── TestableOfflineAwareOrderService ─────────────────────────────────────

    /// <summary>
    /// OfflineAwareOrderService ile aynı mantık — MAUI ve ILogger bağımlılıkları kaldırılmış.
    /// IOrderService'i uygular, mock-friendly constructor alır.
    /// </summary>
    public class TestableOfflineAwareOrderService : IOrderService
    {
        private readonly IOrderService _inner;
        private readonly IConnectivityService _conn;
        private readonly InMemoryOfflineQueue _queue;
        private readonly SemaphoreSlim _flushLock = new(1, 1);

        private const string OpCreateOrder = "CreateOrder";
        private const string OpCloseOrder = "CloseOrder";
        private const string OpAddItem = "AddItem";
        private const string OpUpdateQuantity = "UpdateQuantity";
        private const string OpRemoveItem = "RemoveItem";

        public TestableOfflineAwareOrderService(
            IOrderService inner,
            IConnectivityService connectivity,
            InMemoryOfflineQueue queue)
        {
            _inner = inner;
            _conn = connectivity;
            _queue = queue;

            _conn.ConnectivityChanged += async (_, isConnected) =>
            {
                if (isConnected)
                    await FlushQueueAsync();
            };
        }

        // ─── Okuma işlemleri — her zaman DB ──────────────────────────────────

        public Task<BaseResponse<OrderModel?>> GetActiveOrderByTableAsync(string tableId)
            => _inner.GetActiveOrderByTableAsync(tableId);

        public Task<BaseResponse<List<OrderItemModel>>> GetOrderItemsAsync(string orderId)
            => _inner.GetOrderItemsAsync(orderId);

        // ─── Yazma işlemleri — offline ise kuyruğa ───────────────────────────

        public async Task<BaseResponse<OrderModel>> CreateOrderAsync(string tableId)
        {
            if (_conn.IsConnected)
                return await _inner.CreateOrderAsync(tableId);

            await _queue.EnqueueAsync(OpCreateOrder, tableId);
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

            await _queue.EnqueueAsync(OpCloseOrder, request);
            return BaseResponse<object>.SuccessResult(null, "[Offline] Hesap kapatma kuyruğa alındı");
        }

        public async Task<BaseResponse<OrderItemModel>> AddOrderItemAsync(AddOrderItemRequest request)
        {
            if (_conn.IsConnected)
                return await _inner.AddOrderItemAsync(request);

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

            await _queue.EnqueueAsync(OpUpdateQuantity, request);
            return BaseResponse<object>.SuccessResult(null, "[Offline] Miktar güncellemesi kuyruğa alındı");
        }

        public async Task<BaseResponse<object>> RemoveOrderItemAsync(string itemId)
        {
            if (_conn.IsConnected)
                return await _inner.RemoveOrderItemAsync(itemId);

            await _queue.EnqueueAsync(OpRemoveItem, itemId);
            return BaseResponse<object>.SuccessResult(null, "[Offline] Kaldırma işlemi kuyruğa alındı");
        }

        // ─── Kuyruk boşaltma ──────────────────────────────────────────────────

        public async Task FlushQueueAsync()
        {
            if (!await _flushLock.WaitAsync(0))
                return; // zaten çalışıyor

            try
            {
                var items = await _queue.GetAllAsync();
                foreach (var item in items)
                {
                    if (!_conn.IsConnected) return;

                    try
                    {
                        var success = await ExecuteAsync(item);
                        if (success)
                            await _queue.RemoveAsync(item.Id);
                        else
                            await _queue.IncrementRetryAsync(item.Id);
                    }
                    catch
                    {
                        await _queue.IncrementRetryAsync(item.Id);
                    }
                }
            }
            finally { _flushLock.Release(); }
        }

        private async Task<bool> ExecuteAsync(InMemoryOfflineQueue.QueueItem item)
        {
            switch (item.Operation)
            {
                case OpCreateOrder:
                    {
                        var tableId = InMemoryOfflineQueue.Deserialize<string>(item.Payload);
                        var r = await _inner.CreateOrderAsync(tableId);
                        return r.Success;
                    }
                case OpCloseOrder:
                    {
                        var req = InMemoryOfflineQueue.Deserialize<CloseOrderRequest>(item.Payload);
                        var r = await _inner.CloseOrderAsync(req);
                        return r.Success;
                    }
                case OpAddItem:
                    {
                        var req = InMemoryOfflineQueue.Deserialize<AddOrderItemRequest>(item.Payload);
                        var r = await _inner.AddOrderItemAsync(req);
                        return r.Success;
                    }
                case OpUpdateQuantity:
                    {
                        var req = InMemoryOfflineQueue.Deserialize<UpdateOrderItemQuantityRequest>(item.Payload);
                        var r = await _inner.UpdateOrderItemQuantityAsync(req);
                        return r.Success;
                    }
                case OpRemoveItem:
                    {
                        var itemId = InMemoryOfflineQueue.Deserialize<string>(item.Payload);
                        var r = await _inner.RemoveOrderItemAsync(itemId);
                        return r.Success;
                    }
                default:
                    return true;
            }
        }
    }
}