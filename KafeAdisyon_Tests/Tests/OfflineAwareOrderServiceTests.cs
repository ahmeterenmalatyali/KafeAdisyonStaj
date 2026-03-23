using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Models;
using KafeAdisyon.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace KafeAdisyon.Tests
{
    /// <summary>
    /// TestableOfflineAwareOrderService testleri.
    ///
    /// Test grupları:
    ///   Online  — tüm yazma/okuma işlemleri inner servise geçilir.
    ///   Offline — yazma işlemleri kuyruğa alınır; okuma işlemleri yine inner'a gider.
    ///   Flush   — kuyruk boşaltma mantığı.
    ///   Event   — ConnectivityChanged eventi flush'u tetikler.
    /// </summary>
    public class OfflineAwareOrderServiceTests
    {
        // ─── Yardımcılar ──────────────────────────────────────────────────────

        private Mock<IOrderService> InnerMock() => new();

        private (TestableOfflineAwareOrderService svc,
                 Mock<IOrderService> inner,
                 FakeConnectivityService conn,
                 InMemoryOfflineQueue queue)
            Build(bool isConnected = true)
        {
            var inner = InnerMock();
            var conn = new FakeConnectivityService(isConnected);
            var queue = new InMemoryOfflineQueue();
            var svc = new TestableOfflineAwareOrderService(inner.Object, conn, queue);
            return (svc, inner, conn, queue);
        }

        private static AddOrderItemRequest SampleAddReq(string orderId = "o1") => new()
        {
            OrderId = orderId,
            MenuItemId = "m1",
            Quantity = 2,
            Price = 45.0
        };

        private static CloseOrderRequest SampleCloseReq(string orderId = "o1") => new()
        {
            OrderId = orderId,
            TableId = "t1",
            FinalTotal = 90.0
        };

        // ─── Online: yazma işlemleri inner'a geçilir ──────────────────────────

        [Fact(DisplayName = "Online: CreateOrder — inner servise iletilir")]
        public async Task Online_CreateOrder_DelegatesToInner()
        {
            var (svc, inner, _, queue) = Build(isConnected: true);
            var expected = new OrderModel { Id = "o1", TableId = "t1", Status = "aktif" };
            inner.Setup(x => x.CreateOrderAsync("t1"))
                 .ReturnsAsync(BaseResponse<OrderModel>.SuccessResult(expected));

            var result = await svc.CreateOrderAsync("t1");

            result.Success.Should().BeTrue();
            result.Data!.Id.Should().Be("o1", "gerçek DB ID dönmeli");
            (await queue.CountAsync()).Should().Be(0, "online iken kuyruk kullanılmaz");
            inner.Verify(x => x.CreateOrderAsync("t1"), Times.Once);
        }

        [Fact(DisplayName = "Online: AddOrderItem — inner servise iletilir")]
        public async Task Online_AddOrderItem_DelegatesToInner()
        {
            var (svc, inner, _, queue) = Build();
            var req = SampleAddReq();
            inner.Setup(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()))
                 .ReturnsAsync(BaseResponse<OrderItemModel>.SuccessResult(new OrderItemModel { Id = "oi1" }));

            var result = await svc.AddOrderItemAsync(req);

            result.Success.Should().BeTrue();
            result.Data!.Id.Should().Be("oi1");
            (await queue.CountAsync()).Should().Be(0);
        }

        [Fact(DisplayName = "Online: CloseOrder — inner servise iletilir")]
        public async Task Online_CloseOrder_DelegatesToInner()
        {
            var (svc, inner, _, queue) = Build();
            inner.Setup(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()))
                 .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            var result = await svc.CloseOrderAsync(SampleCloseReq());

            result.Success.Should().BeTrue();
            (await queue.CountAsync()).Should().Be(0);
            inner.Verify(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()), Times.Once);
        }

        [Fact(DisplayName = "Online: UpdateQuantity — inner servise iletilir")]
        public async Task Online_UpdateQuantity_DelegatesToInner()
        {
            var (svc, inner, _, _) = Build();
            inner.Setup(x => x.UpdateOrderItemQuantityAsync(It.IsAny<UpdateOrderItemQuantityRequest>()))
                 .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await svc.UpdateOrderItemQuantityAsync(new UpdateOrderItemQuantityRequest
            { ItemId = "oi1", Quantity = 5 });

            inner.Verify(x => x.UpdateOrderItemQuantityAsync(It.Is<UpdateOrderItemQuantityRequest>(r =>
                r.ItemId == "oi1" && r.Quantity == 5)), Times.Once);
        }

        [Fact(DisplayName = "Online: RemoveOrderItem — inner servise iletilir")]
        public async Task Online_RemoveOrderItem_DelegatesToInner()
        {
            var (svc, inner, _, _) = Build();
            inner.Setup(x => x.RemoveOrderItemAsync("oi1"))
                 .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await svc.RemoveOrderItemAsync("oi1");

            inner.Verify(x => x.RemoveOrderItemAsync("oi1"), Times.Once);
        }

        // ─── Online / Offline: okuma işlemleri her zaman inner'a gider ────────

        [Fact(DisplayName = "Offline: GetActiveOrderByTable — okuma yine inner'a gider")]
        public async Task Offline_GetActiveOrder_StillDelegatesToInner()
        {
            var (svc, inner, _, _) = Build(isConnected: false);
            inner.Setup(x => x.GetActiveOrderByTableAsync("t1"))
                 .ReturnsAsync(BaseResponse<OrderModel?>.SuccessResult(null));

            await svc.GetActiveOrderByTableAsync("t1");

            inner.Verify(x => x.GetActiveOrderByTableAsync("t1"), Times.Once);
        }

        [Fact(DisplayName = "Offline: GetOrderItems — okuma yine inner'a gider")]
        public async Task Offline_GetOrderItems_StillDelegatesToInner()
        {
            var (svc, inner, _, _) = Build(isConnected: false);
            inner.Setup(x => x.GetOrderItemsAsync("o1"))
                 .ReturnsAsync(BaseResponse<List<OrderItemModel>>.SuccessResult(new()));

            await svc.GetOrderItemsAsync("o1");

            inner.Verify(x => x.GetOrderItemsAsync("o1"), Times.Once);
        }

        // ─── Offline: yazma işlemleri kuyruğa alınır ─────────────────────────

        [Fact(DisplayName = "Offline: CreateOrder — offline model döner, kuyrukta 1 öğe")]
        public async Task Offline_CreateOrder_ReturnsOfflineModel_EnqueuesOperation()
        {
            var (svc, inner, _, queue) = Build(isConnected: false);

            var result = await svc.CreateOrderAsync("t1");

            result.Success.Should().BeTrue();
            result.Data!.Id.Should().StartWith("_offline_",
                "bağlantı yokken geçici ID atanmalı");
            result.Data.TableId.Should().Be("t1");
            result.Data.Status.Should().Be("aktif");

            (await queue.CountAsync()).Should().Be(1);
            var qItem = (await queue.GetAllAsync())[0];
            qItem.Operation.Should().Be("CreateOrder");

            inner.Verify(x => x.CreateOrderAsync(It.IsAny<string>()), Times.Never,
                "offline iken inner servis çağrılmamalı");
        }

        [Fact(DisplayName = "Offline: AddOrderItem — offline item döner, kuyrukta 1 öğe")]
        public async Task Offline_AddOrderItem_ReturnsOfflineItem_EnqueuesOperation()
        {
            var (svc, inner, _, queue) = Build(isConnected: false);
            var req = SampleAddReq("o1");

            var result = await svc.AddOrderItemAsync(req);

            result.Success.Should().BeTrue();
            result.Data!.Id.Should().StartWith("_offline_");
            result.Data.Quantity.Should().Be(2);
            result.Data.Price.Should().Be(45.0);

            (await queue.CountAsync()).Should().Be(1);
            var qItem = (await queue.GetAllAsync())[0];
            qItem.Operation.Should().Be("AddItem");

            inner.Verify(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()), Times.Never);
        }

        [Fact(DisplayName = "Offline: CloseOrder — başarılı yanıt döner, kuyruğa alınır")]
        public async Task Offline_CloseOrder_ReturnsSuccess_EnqueuesOperation()
        {
            var (svc, inner, _, queue) = Build(isConnected: false);

            var result = await svc.CloseOrderAsync(SampleCloseReq());

            result.Success.Should().BeTrue();
            (await queue.CountAsync()).Should().Be(1);
            inner.Verify(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()), Times.Never);
        }

        [Fact(DisplayName = "Offline: UpdateQuantity — başarılı yanıt döner, kuyruğa alınır")]
        public async Task Offline_UpdateQuantity_ReturnsSuccess_EnqueuesOperation()
        {
            var (svc, _, _, queue) = Build(isConnected: false);

            var result = await svc.UpdateOrderItemQuantityAsync(
                new UpdateOrderItemQuantityRequest { ItemId = "oi1", Quantity = 3 });

            result.Success.Should().BeTrue();
            (await queue.CountAsync()).Should().Be(1);
        }

        [Fact(DisplayName = "Offline: RemoveOrderItem — başarılı yanıt döner, kuyruğa alınır")]
        public async Task Offline_RemoveOrderItem_ReturnsSuccess_EnqueuesOperation()
        {
            var (svc, _, _, queue) = Build(isConnected: false);

            var result = await svc.RemoveOrderItemAsync("oi1");

            result.Success.Should().BeTrue();
            (await queue.CountAsync()).Should().Be(1);
        }

        [Fact(DisplayName = "Offline: Birden fazla işlem sırayla kuyruğa eklenir")]
        public async Task Offline_MultipleOperations_AllEnqueued()
        {
            var (svc, _, _, queue) = Build(isConnected: false);

            await svc.CreateOrderAsync("t1");
            await svc.AddOrderItemAsync(SampleAddReq("o1"));
            await svc.AddOrderItemAsync(SampleAddReq("o1"));
            await svc.CloseOrderAsync(SampleCloseReq("o1"));

            (await queue.CountAsync()).Should().Be(4, "4 işlemin tamamı kuyruğa alınmalı");

            var items = await queue.GetAllAsync();
            items[0].Operation.Should().Be("CreateOrder");
            items[1].Operation.Should().Be("AddItem");
            items[2].Operation.Should().Be("AddItem");
            items[3].Operation.Should().Be("CloseOrder");
        }

        // ─── Flush ────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Flush: Tüm kuyruktaki işlemler inner servise iletilir, kuyruk temizlenir")]
        public async Task Flush_AllQueuedItems_ExecutedAndQueueCleared()
        {
            var (svc, inner, conn, queue) = Build(isConnected: false);

            // Offline iken 2 işlem kuyruğa al
            await svc.CreateOrderAsync("t1");
            await svc.AddOrderItemAsync(SampleAddReq("o1"));

            (await queue.CountAsync()).Should().Be(2);

            // İnner mock'ları ayarla
            inner.Setup(x => x.CreateOrderAsync(It.IsAny<string>()))
                 .ReturnsAsync(BaseResponse<OrderModel>.SuccessResult(new OrderModel { Id = "o1" }));
            inner.Setup(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()))
                 .ReturnsAsync(BaseResponse<OrderItemModel>.SuccessResult(new OrderItemModel { Id = "oi1" }));

            // Bağlantıyı kur ve flush et
            conn.SetConnected(true);
            await Task.Delay(50); // event handler'ın çalışması için küçük bekleme

            inner.Verify(x => x.CreateOrderAsync(It.IsAny<string>()), Times.Once);
            inner.Verify(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()), Times.Once);
            (await queue.CountAsync()).Should().Be(0, "başarılı işlemler kuyruktan temizlenmeli");
        }

        [Fact(DisplayName = "Flush: Inner servis hata dönünce retry sayacı artar, öğe kuyrukta kalır")]
        public async Task Flush_FailedItem_IncreasesRetryCount_ItemRemainsInQueue()
        {
            var (svc, inner, _, queue) = Build(isConnected: false);
            await svc.AddOrderItemAsync(SampleAddReq());

            // Bağlantı var ama inner hata dönüyor
            inner.Setup(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()))
                 .ReturnsAsync(BaseResponse<OrderItemModel>.ErrorResult("DB zaman aşımı"));

            var connectedSvc = new TestableOfflineAwareOrderService(
                inner.Object,
                new FakeConnectivityService(true),
                queue);

            await connectedSvc.FlushQueueAsync();

            var items = await queue.GetAllAsync();
            items.Should().HaveCount(1, "başarısız öğe kuyrukta kalmalı");
            items[0].RetryCount.Should().Be(1, "bir deneme yapıldı");
        }

        [Fact(DisplayName = "Flush: Bağlantı düşünce durur, kuyruk kısmen işlenir")]
        public async Task Flush_ConnectivityDrops_StopsProcessing()
        {
            var inner = InnerMock();
            var conn = new FakeConnectivityService(true);
            var queue = new InMemoryOfflineQueue();

            // 3 öğeyi doğrudan kuyruğa ekle (offline servis yerine doğrudan)
            await queue.EnqueueAsync("AddItem", new AddOrderItemRequest { OrderId = "o1", MenuItemId = "m1", Quantity = 1, Price = 10 });
            await queue.EnqueueAsync("AddItem", new AddOrderItemRequest { OrderId = "o1", MenuItemId = "m2", Quantity = 1, Price = 20 });
            await queue.EnqueueAsync("AddItem", new AddOrderItemRequest { OrderId = "o1", MenuItemId = "m3", Quantity = 1, Price = 30 });

            var callCount = 0;
            inner.Setup(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()))
                 .ReturnsAsync(() =>
                 {
                     callCount++;
                     if (callCount == 1)
                         conn.SetConnected(false); // ilk çağrıdan sonra bağlantı kesilir
                     return BaseResponse<OrderItemModel>.SuccessResult(new OrderItemModel { Id = "oi" + callCount });
                 });

            var svc = new TestableOfflineAwareOrderService(inner.Object, conn, queue);
            await svc.FlushQueueAsync();

            inner.Verify(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()),
                Times.AtMost(2), "bağlantı kesildikten sonra daha fazla işlem yapılmamalı");
        }

        [Fact(DisplayName = "Flush: Eşzamanlı FlushQueueAsync çağrıları çift iş yapmaz")]
        public async Task Flush_ConcurrentCalls_OnlyOneExecutes()
        {
            var (svc, inner, _, queue) = Build(isConnected: false);
            await svc.AddOrderItemAsync(SampleAddReq());

            var callCount = 0;
            inner.Setup(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()))
                 .Returns(async () =>
                 {
                     callCount++;
                     await Task.Delay(30); // biraz beklet
                     return BaseResponse<OrderItemModel>.SuccessResult(new OrderItemModel { Id = "oi1" });
                 });

            // Bağlantıyı açık olarak yeni servis — doğrudan flush çağır
            var connectedSvc = new TestableOfflineAwareOrderService(
                inner.Object,
                new FakeConnectivityService(true),
                queue);

            var tasks = Enumerable.Range(0, 5).Select(_ => connectedSvc.FlushQueueAsync()).ToArray();
            await Task.WhenAll(tasks);

            callCount.Should().Be(1, "SemaphoreSlim eş zamanlı flush'ları engellemelidir");
        }

        // ─── ConnectivityChanged event ────────────────────────────────────────

        [Fact(DisplayName = "Event: Bağlantı kurulunca kuyruk otomatik flush edilir")]
        public async Task Event_ConnectivityRestored_TriggersFlush()
        {
            var (svc, inner, conn, queue) = Build(isConnected: false);

            await svc.AddOrderItemAsync(SampleAddReq());
            (await queue.CountAsync()).Should().Be(1);

            inner.Setup(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()))
                 .ReturnsAsync(BaseResponse<OrderItemModel>.SuccessResult(new OrderItemModel { Id = "oi1" }));

            conn.SetConnected(true);
            await Task.Delay(100); // event handler async — biraz bekle

            inner.Verify(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()), Times.Once,
                "bağlantı gelince flush tetiklenmeli");
        }

        [Fact(DisplayName = "Event: Bağlantı kesilince flush tetiklenmez")]
        public async Task Event_ConnectivityLost_DoesNotTriggerFlush()
        {
            var (svc, inner, conn, _) = Build(isConnected: true);

            conn.SetConnected(false); // bağlantı kesildi

            inner.Verify(x => x.AddOrderItemAsync(It.IsAny<AddOrderItemRequest>()), Times.Never,
                "bağlantı kesilmesi flush başlatmamalı");
        }

        [Fact(DisplayName = "Event: Flush sonrası kuyruk boş olduğunda yeniden flush zararsız")]
        public async Task Event_FlushOnEmptyQueue_NoError()
        {
            var (svc, _, conn, queue) = Build(isConnected: false);

            // Kuyruk boş iken bağlantı gelsin
            conn.SetConnected(true);
            await Task.Delay(50);

            (await queue.CountAsync()).Should().Be(0);
        }
    }
}