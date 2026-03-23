using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.IntegrationTests.Infrastructure;
using KafeAdisyon.Models;
using Xunit;

namespace KafeAdisyon.IntegrationTests.Tests.Integration
{
    /// <summary>
    /// Offline → flush → Supabase doğrulama testleri.
    ///
    /// Her test:
    ///   1. Gerçek Supabase bağlantısı kullanır (DatabaseFixture).
    ///   2. FakeConnectivityService ile bağlantıyı simüle eder.
    ///   3. InMemoryOfflineQueue ile MAUI Preferences bağımlılığını bypass eder.
    ///   4. TestableOfflineAwareOrderService aracılığıyla offline işlem kuyruğa alır.
    ///   5. Bağlantı "gelince" kuyruk flush edilir → Supabase'de doğrulanır.
    /// </summary>
    [Collection("Database")]
    public class OfflineOrderServiceIntegrationTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fx;

        public OfflineOrderServiceIntegrationTests(DatabaseFixture fx) => _fx = fx;

        // ─── Yardımcılar ──────────────────────────────────────────────────────

        private (TestableOfflineAwareOrderService svc,
                 FakeConnectivityService conn,
                 InMemoryOfflineQueue queue)
            BuildOfflineService(bool startConnected = false)
        {
            var conn = new FakeConnectivityService(startConnected);
            var queue = new InMemoryOfflineQueue();
            var svc = new TestableOfflineAwareOrderService(_fx.OrderService, conn, queue);
            return (svc, conn, queue);
        }

        private async Task<TableModel> GetAnyTable()
        {
            var r = await _fx.TableService.GetAllTablesAsync();
            r.Success.Should().BeTrue(r.Message);
            r.Data.Should().NotBeEmpty();
            return r.Data!.First();
        }

        private async Task<MenuItemModel> GetAnyMenuItem()
        {
            var r = await _fx.MenuService.GetAllMenuItemsAsync();
            r.Success.Should().BeTrue(r.Message);
            r.Data.Should().NotBeEmpty();
            return r.Data!.First();
        }

        // ─── Kuyruk davranışı ─────────────────────────────────────────────────

        [Fact(DisplayName = "DB | Offline: Bağlantı yokken AddOrderItem kuyruğa alınır, DB'ye yazılmaz")]
        public async Task Offline_AddItem_GoesToQueue_NotToDatabase()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();
            var (svc, _, queue) = BuildOfflineService(startConnected: false);

            // Gerçek Supabase'de sipariş aç (online)
            var orderResp = await _fx.OrderService.CreateOrderAsync(table.Id);
            orderResp.Success.Should().BeTrue(orderResp.Message);
            var order = orderResp.Data!;
            _fx.TrackOrder(order.Id);

            // Offline servis ile kalem ekle
            var addResp = await svc.AddOrderItemAsync(new AddOrderItemRequest
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = 3,
                Price = menuItem.Price
            });

            // Yanıt optimistic — başarılı ama _offline_ prefixli
            addResp.Success.Should().BeTrue();
            addResp.Data!.Id.Should().StartWith("_offline_",
                "bağlantı yokken geçici ID atanmalı");

            // Kuyrukta 1 öğe var
            (await queue.CountAsync()).Should().Be(1);

            // Supabase'de henüz sipariş kalemi YOK
            var itemsBefore = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            itemsBefore.Success.Should().BeTrue(itemsBefore.Message);
            itemsBefore.Data!.Should().BeEmpty(
                "offline iken DB'ye hiçbir şey yazılmamalı");
        }

        [Fact(DisplayName = "DB | Offline: Bağlantı gelince kuyruk flush edilir, kalem Supabase'de oluşur")]
        public async Task Offline_AddItem_ThenFlush_ItemCreatedInDatabase()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();
            var (svc, conn, queue) = BuildOfflineService(startConnected: false);

            // Gerçek sipariş aç
            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            // Offline: ürün ekle
            await svc.AddOrderItemAsync(new AddOrderItemRequest
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = 2,
                Price = menuItem.Price
            });

            (await queue.CountAsync()).Should().Be(1, "flush öncesi kuyrukta bekleniyor");

            // Bağlantıyı getir → flush tetiklenir
            conn.SetConnected(true);
            await Task.Delay(1500); // Supabase çağrısı ~400ms, yeterli marj bırakılıyor

            // Kuyruk temizlendi
            (await queue.CountAsync()).Should().Be(0, "başarılı flush sonrası kuyruk boş olmalı");

            // Supabase'de kalem var
            var itemsAfter = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            itemsAfter.Success.Should().BeTrue(itemsAfter.Message);
            itemsAfter.Data!.Should().HaveCount(1, "flush sonrası DB'de 1 kalem görünmeli");
            itemsAfter.Data![0].Quantity.Should().Be(2);
            itemsAfter.Data![0].MenuItemId.Should().Be(menuItem.Id);
        }

        [Fact(DisplayName = "DB | Offline: Tam akış — offline sipariş aç, ürün ekle, flush, kapat")]
        public async Task Offline_FullFlow_CreateAddFlushClose_AllPersistedToDatabase()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();

            // Önce mevcut aktif siparişi temizle
            var existing = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (existing.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = existing.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var (svc, conn, queue) = BuildOfflineService(startConnected: false);

            // ─── Faz 1: Offline — 2 işlem kuyruğa al ────────────────────────

            // Online'da sipariş aç (sadece order_id almak için; bu direkt DB'ye gider)
            var orderResp = await _fx.OrderService.CreateOrderAsync(table.Id);
            orderResp.Success.Should().BeTrue(orderResp.Message);
            var order = orderResp.Data!;
            _fx.TrackOrder(order.Id);

            // Offline servis ile ürün 1 ekle
            await svc.AddOrderItemAsync(new AddOrderItemRequest
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = 2,
                Price = menuItem.Price
            });

            // Offline servis ile ürün 2 ekle (aynı ürün, farklı satır olarak)
            await svc.AddOrderItemAsync(new AddOrderItemRequest
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = 1,
                Price = menuItem.Price
            });

            (await queue.CountAsync()).Should().Be(2, "2 offline işlem bekliyor");

            var itemsBeforeFlush = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            itemsBeforeFlush.Data!.Should().BeEmpty("flush öncesi DB'de kalem olmamalı");

            // ─── Faz 2: Bağlantı geldi → flush ───────────────────────────────

            conn.SetConnected(true);
            await Task.Delay(1500); // flush async çalışıyor, Supabase ~400ms × 2 işlem

            (await queue.CountAsync()).Should().Be(0);

            // ─── Faz 3: Supabase doğrulama ────────────────────────────────────

            var itemsAfterFlush = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            itemsAfterFlush.Success.Should().BeTrue(itemsAfterFlush.Message);
            itemsAfterFlush.Data!.Should().HaveCount(2, "iki offline ürün de DB'ye yazılmış olmalı");

            double dbTotal = itemsAfterFlush.Data!.Sum(i => i.Price * i.Quantity);
            double expectedTotal = menuItem.Price * 3; // 2 + 1

            dbTotal.Should().BeApproximately(expectedTotal, 0.001);

            // Siparişi kapat
            var closeResp = await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
            {
                OrderId = order.Id,
                TableId = table.Id,
                FinalTotal = expectedTotal
            });
            closeResp.Success.Should().BeTrue(closeResp.Message);
        }

        [Fact(DisplayName = "DB | Offline: Manuel FlushQueueAsync çağrısı event gelmese de çalışır")]
        public async Task Offline_ManualFlush_WorksWithoutConnectivityEvent()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();
            var (svc, conn, queue) = BuildOfflineService(startConnected: false);

            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            await svc.AddOrderItemAsync(new AddOrderItemRequest
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = 1,
                Price = menuItem.Price
            });

            // Event olmadan bağlantıyı açık yap ve manuel flush
            // NOT: SetConnected(true) yerine SetConnectedSilently kullanılıyor.
            // SetConnected event'i tetikler → async handler FlushQueueAsync lock'u alır →
            // hemen arkasındaki manuel FlushQueueAsync çağrısı WaitAsync(0) ile lock'u
            // alamaz ve boş döner. SetConnectedSilently ile bu yarış koşulu ortadan kalkar.
            conn.SetConnectedSilently(true);
            await svc.FlushQueueAsync();

            (await queue.CountAsync()).Should().Be(0);

            var items = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            items.Data!.Should().HaveCount(1, "manuel flush DB'ye yazmalı");
        }

        [Fact(DisplayName = "DB | Offline: Flush sırasında bağlantı kesilirse kalan öğeler kuyrukta kalır")]
        public async Task Offline_FlushDropsConnection_RemainingItemsStayInQueue()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();

            // Gerçek order aç
            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            var conn = new FakeConnectivityService(true);
            var queue = new InMemoryOfflineQueue();

            // 3 öğeyi doğrudan kuyruğa yaz
            for (int i = 0; i < 3; i++)
                await queue.EnqueueAsync("AddItem", new AddOrderItemRequest
                { OrderId = order.Id, MenuItemId = menuItem.Id, Quantity = 1, Price = menuItem.Price });

            var processedCount = 0;

            // Özel inner: 1. işlemde bağlantıyı kes
            var fakeInner = new FakeOrderService(async req =>
            {
                processedCount++;
                var real = await _fx.OrderService.AddOrderItemAsync(req);
                if (processedCount == 1)
                    conn.SetConnected(false); // ilk başarılı işlemden sonra kes
                return real;
            });

            var svc = new TestableOfflineAwareOrderService(fakeInner, conn, queue);
            await svc.FlushQueueAsync();

            // En az 1 işlem yapıldı, kalan 2 hâlâ kuyrukta
            processedCount.Should().BeGreaterThanOrEqualTo(1);
            (await queue.CountAsync()).Should().BeGreaterThan(0,
                "bağlantı kesilince kalan öğeler kuyrukta kalmalı");
        }

        // ─── Yardımcı fake inner servis ──────────────────────────────────────

        /// <summary>
        /// AddOrderItemAsync için özelleştirilebilir davranış sağlar.
        /// Diğer metodlar _fx.OrderService'e iletilir.
        /// </summary>
        private class FakeOrderService : KafeAdisyon.Application.Interfaces.IOrderService
        {
            private readonly Func<AddOrderItemRequest, Task<KafeAdisyon.Common.BaseResponse<KafeAdisyon.Models.OrderItemModel>>> _addHandler;

            public FakeOrderService(
                Func<AddOrderItemRequest, Task<KafeAdisyon.Common.BaseResponse<KafeAdisyon.Models.OrderItemModel>>> addHandler)
            {
                _addHandler = addHandler;
            }

            public Task<KafeAdisyon.Common.BaseResponse<KafeAdisyon.Models.OrderModel?>> GetActiveOrderByTableAsync(string tableId)
                => Task.FromResult(KafeAdisyon.Common.BaseResponse<KafeAdisyon.Models.OrderModel?>.SuccessResult(null));

            public Task<KafeAdisyon.Common.BaseResponse<KafeAdisyon.Models.OrderModel>> CreateOrderAsync(string tableId)
                => Task.FromResult(KafeAdisyon.Common.BaseResponse<KafeAdisyon.Models.OrderModel>.SuccessResult(new KafeAdisyon.Models.OrderModel()));

            public Task<KafeAdisyon.Common.BaseResponse<object>> CloseOrderAsync(CloseOrderRequest request)
                => Task.FromResult(KafeAdisyon.Common.BaseResponse<object>.SuccessResult(null));

            public Task<KafeAdisyon.Common.BaseResponse<List<KafeAdisyon.Models.OrderItemModel>>> GetOrderItemsAsync(string orderId)
                => Task.FromResult(KafeAdisyon.Common.BaseResponse<List<KafeAdisyon.Models.OrderItemModel>>.SuccessResult(new List<KafeAdisyon.Models.OrderItemModel>()));

            public Task<KafeAdisyon.Common.BaseResponse<KafeAdisyon.Models.OrderItemModel>> AddOrderItemAsync(AddOrderItemRequest request)
                => _addHandler(request);

            public Task<KafeAdisyon.Common.BaseResponse<object>> UpdateOrderItemQuantityAsync(UpdateOrderItemQuantityRequest request)
                => Task.FromResult(KafeAdisyon.Common.BaseResponse<object>.SuccessResult(null));

            public Task<KafeAdisyon.Common.BaseResponse<object>> RemoveOrderItemAsync(string itemId)
                => Task.FromResult(KafeAdisyon.Common.BaseResponse<object>.SuccessResult(null));
        }
    }
}