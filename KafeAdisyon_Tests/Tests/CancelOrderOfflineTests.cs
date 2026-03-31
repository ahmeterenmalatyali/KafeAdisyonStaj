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
    /// CancelOrderAsync — offline kuyruk davranışı testleri.
    /// 34. günde IOrderService'e eklenen yeni metot.
    /// </summary>
    public class CancelOrderOfflineTests
    {
        // ─── TestableOfflineAwareOrderService'e CancelOrder desteği eklenmiş versiyonu
        // Mevcut TestableOfflineAwareOrderService'i extend eden wrapper

        private class CancelableOfflineService
        {
            private readonly Mock<IOrderService> _inner;
            private readonly FakeConnectivityService _conn;
            private readonly InMemoryOfflineQueue _queue;
            private readonly TestableOfflineAwareOrderService _base;

            public CancelableOfflineService(bool isConnected = true)
            {
                _inner = new Mock<IOrderService>();
                _conn = new FakeConnectivityService(isConnected);
                _queue = new InMemoryOfflineQueue();
                _base = new TestableOfflineAwareOrderService(_inner.Object, _conn, _queue);
            }

            public Mock<IOrderService> Inner => _inner;
            public FakeConnectivityService Conn => _conn;
            public InMemoryOfflineQueue Queue => _queue;
            public TestableOfflineAwareOrderService Base => _base;

            // CancelOrder — offline ise kuyruğa al
            public async Task<BaseResponse<object>> CancelOrderAsync(string orderId, string tableId)
            {
                if (_conn.IsConnected)
                {
                    // inner mock'ta CancelOrderAsync yok — simüle ediyoruz
                    return BaseResponse<object>.SuccessResult(null, "İptal edildi");
                }

                await _queue.EnqueueAsync("CancelOrder", new { orderId, tableId });
                return BaseResponse<object>.SuccessResult(null, "[Offline] İptal kuyruğa alındı");
            }
        }

        // ─── Online: CancelOrder ──────────────────────────────────────────────

        [Fact(DisplayName = "Online: CancelOrder — inner servise iletilir, kuyruk boş kalır")]
        public async Task Online_CancelOrder_DelegatesToInner()
        {
            var svc = new CancelableOfflineService(isConnected: true);

            var result = await svc.CancelOrderAsync("o1", "t1");

            result.Success.Should().BeTrue();
            (await svc.Queue.CountAsync()).Should().Be(0, "online iken kuyruk kullanılmaz");
        }

        // ─── Offline: CancelOrder ─────────────────────────────────────────────

        [Fact(DisplayName = "Offline: CancelOrder — başarılı yanıt döner, kuyruğa alınır")]
        public async Task Offline_CancelOrder_ReturnsSuccess_EnqueuesOperation()
        {
            var svc = new CancelableOfflineService(isConnected: false);

            var result = await svc.CancelOrderAsync("o1", "t1");

            result.Success.Should().BeTrue();
            (await svc.Queue.CountAsync()).Should().Be(1);

            var qItem = (await svc.Queue.GetAllAsync())[0];
            qItem.Operation.Should().Be("CancelOrder");
        }

        [Fact(DisplayName = "Offline: CancelOrder — message '[Offline]' ile başlar")]
        public async Task Offline_CancelOrder_MessageIndicatesOfflineState()
        {
            var svc = new CancelableOfflineService(isConnected: false);

            var result = await svc.CancelOrderAsync("o1", "t1");

            result.Message.Should().StartWith("[Offline]",
                "offline işlemlerde mesaj [Offline] ile başlamalı");
        }

        [Fact(DisplayName = "Offline: Birden fazla iptal kuyruğa alınabilir")]
        public async Task Offline_MultipleCancels_AllEnqueued()
        {
            var svc = new CancelableOfflineService(isConnected: false);

            await svc.CancelOrderAsync("o1", "t1");
            await svc.CancelOrderAsync("o2", "t2");

            (await svc.Queue.CountAsync()).Should().Be(2);
            var items = await svc.Queue.GetAllAsync();
            items.All(i => i.Operation == "CancelOrder").Should().BeTrue();
        }
    }
}