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
    public class OrderViewModelTests
    {
        private Mock<IMenuService> MenuMock() => new Mock<IMenuService>();
        private Mock<IOrderService> OrderMock() => new Mock<IOrderService>();
        private Mock<ITableService> TableMock() => new Mock<ITableService>();

        private static List<MenuItemModel> DefaultMenu() => new()
        {
            TestFactory.Menu("m1", "Türk Kahvesi", 45),
            TestFactory.Menu("m2", "Cappuccino", 60),
            TestFactory.Menu("m3", "Su", 10)
        };

        private (OrderViewModel vm, Mock<IMenuService> ms, Mock<IOrderService> os, Mock<ITableService> ts)
            BuildVm(List<MenuItemModel>? menu = null)
        {
            var ms = MenuMock();
            var os = OrderMock();
            var ts = TableMock();
            ms.Setup(x => x.GetAllMenuItemsAsync())
              .ReturnsAsync(BaseResponse<List<MenuItemModel>>.SuccessResult(menu ?? DefaultMenu()));
            var vm = new OrderViewModel(ms.Object, os.Object, ts.Object);
            return (vm, ms, os, ts);
        }

        // ─── LoadAsync ────────────────────────────────────────────────────

        [Fact(DisplayName = "Load: Aktif sipariş yok — CurrentOrder null, Total sıfır")]
        public async Task LoadAsync_NoActiveOrder_CurrentOrderNullTotalZero()
        {
            var (vm, _, os, _) = BuildVm();
            os.Setup(x => x.GetActiveOrderByTableAsync("t1"))
              .ReturnsAsync(BaseResponse<OrderModel?>.SuccessResult(null));

            await vm.LoadAsync("t1", "A-1");

            vm.CurrentOrder.Should().BeNull();
            vm.Total.Should().Be(0);
            vm.OrderItems.Should().BeEmpty();
        }

        [Fact(DisplayName = "Load: Aktif sipariş var ve kalemleri var — yüklenir")]
        public async Task LoadAsync_ActiveOrderWithItems_LoadsCorrectly()
        {
            var (vm, _, os, _) = BuildVm();
            var order = TestFactory.Order("o1", "t1");
            var orderItems = new List<OrderItemModel>
            {
                TestFactory.OrderItem("oi1", "m1", price: 45, qty: 2, orderId: "o1"),
                TestFactory.OrderItem("oi2", "m2", price: 60, qty: 1, orderId: "o1")
            };
            os.Setup(x => x.GetActiveOrderByTableAsync("t1"))
              .ReturnsAsync(BaseResponse<OrderModel?>.SuccessResult(order));
            os.Setup(x => x.GetOrderItemsAsync("o1"))
              .ReturnsAsync(BaseResponse<List<OrderItemModel>>.SuccessResult(orderItems));

            await vm.LoadAsync("t1", "A-1");

            vm.CurrentOrder.Should().NotBeNull();
            vm.OrderItems.Should().HaveCount(2);
            vm.Total.Should().Be(150); // 90 + 60
        }

        [Fact(DisplayName = "Load: Aktif sipariş var ama kalemi boş — sipariş kapatılır")]
        public async Task LoadAsync_ActiveOrderEmptyItems_OrderClosed()
        {
            var (vm, _, os, _) = BuildVm();
            var order = TestFactory.Order("o1", "t1");
            os.Setup(x => x.GetActiveOrderByTableAsync("t1"))
              .ReturnsAsync(BaseResponse<OrderModel?>.SuccessResult(order));
            os.Setup(x => x.GetOrderItemsAsync("o1"))
              .ReturnsAsync(BaseResponse<List<OrderItemModel>>.SuccessResult(new List<OrderItemModel>()));
            os.Setup(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await vm.LoadAsync("t1", "A-1");

            vm.CurrentOrder.Should().BeNull();
            vm.Total.Should().Be(0);
            os.Verify(x => x.CloseOrderAsync(It.Is<CloseOrderRequest>(r => r.OrderId == "o1")), Times.Once);
        }

        [Fact(DisplayName = "Load: Menü servisi hata dönünce StatusMessage set edilir")]
        public async Task LoadAsync_MenuServiceError_StatusMessageSet()
        {
            var ms = MenuMock();
            var os = OrderMock();
            var ts = TableMock();
            ms.Setup(x => x.GetAllMenuItemsAsync())
              .ReturnsAsync(BaseResponse<List<MenuItemModel>>.ErrorResult("Bağlantı hatası"));
            var vm = new OrderViewModel(ms.Object, os.Object, ts.Object);

            await vm.LoadAsync("t1", "A-1");

            vm.StatusMessage.Should().Be("Bağlantı hatası");
        }

        // ─── AddItemOptimistic ────────────────────────────────────────────

        [Fact(DisplayName = "Optimistic add: CurrentOrder null iken ürün eklenmez")]
        public void AddItemOptimistic_NoCurrentOrder_DoesNothing()
        {
            var (vm, _, _, _) = BuildVm();
            vm.CurrentOrder = null;
            var menu = TestFactory.Menu("m1", "Kahve", 45);

            vm.AddItemOptimistic(menu);

            vm.OrderItems.Should().BeEmpty();
            vm.Total.Should().Be(0);
        }

        [Fact(DisplayName = "Optimistic add: Yeni ürün temp ID ile eklenir, toplam güncellenir")]
        public void AddItemOptimistic_NewItem_AddedWithTempId()
        {
            var (vm, _, _, _) = BuildVm();
            vm.CurrentOrder = TestFactory.Order();
            var menu = TestFactory.Menu("m1", "Kahve", 45);

            vm.AddItemOptimistic(menu);

            vm.OrderItems.Should().HaveCount(1);
            vm.OrderItems[0].Id.Should().StartWith("_temp_");
            vm.OrderItems[0].Quantity.Should().Be(1);
            vm.Total.Should().Be(45);
        }

        [Fact(DisplayName = "Optimistic add: Var olan ürün adet artışı yapar, yeni satır açılmaz")]
        public void AddItemOptimistic_ExistingItem_QuantityIncremented()
        {
            var (vm, _, _, _) = BuildVm();
            vm.CurrentOrder = TestFactory.Order();
            var menu = TestFactory.Menu("m1", "Kahve", 45);

            vm.AddItemOptimistic(menu); // qty: 1
            vm.AddItemOptimistic(menu); // qty: 2
            vm.AddItemOptimistic(menu); // qty: 3

            vm.OrderItems.Should().HaveCount(1, "aynı ürün yeni satır açmamalı");
            vm.OrderItems[0].Quantity.Should().Be(3);
            vm.Total.Should().Be(135); // 45 × 3
        }

        [Fact(DisplayName = "Optimistic add: Farklı ürünler ayrı satır açar")]
        public void AddItemOptimistic_DifferentItems_SeparateRows()
        {
            var (vm, _, _, _) = BuildVm();
            vm.CurrentOrder = TestFactory.Order();

            vm.AddItemOptimistic(TestFactory.Menu("m1", "Kahve", 45));
            vm.AddItemOptimistic(TestFactory.Menu("m2", "Su", 10));
            vm.AddItemOptimistic(TestFactory.Menu("m1", "Kahve", 45)); // m1 tekrar

            vm.OrderItems.Should().HaveCount(2);
            vm.Total.Should().Be(100); // 90 + 10
        }

        // ─── RemoveItemAsync ──────────────────────────────────────────────

        [Fact(DisplayName = "Remove: Adet 2'den büyükse adet azaltılır, satır kalmaz")]
        public async Task RemoveItemAsync_QtyGreaterThanOne_DecreasesQty()
        {
            var (vm, _, os, _) = BuildVm();
            vm.CurrentOrder = TestFactory.Order();
            var item = TestFactory.OrderItem("oi1", "m1", price: 50, qty: 3);
            vm.OrderItems.Add(item);
            vm.RecalcTotal(); // 150

            os.Setup(x => x.UpdateOrderItemQuantityAsync(It.IsAny<UpdateOrderItemQuantityRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await vm.RemoveItemAsync(item);

            item.Quantity.Should().Be(2);
            vm.Total.Should().Be(100);
            vm.OrderItems.Should().HaveCount(1, "satır kaldırılmamış olmalı");
        }

        [Fact(DisplayName = "Remove: Adet 1 iken ürün listeden silinir")]
        public async Task RemoveItemAsync_QtyOne_ItemRemovedFromList()
        {
            var (vm, _, os, _) = BuildVm();
            vm.CurrentOrder = TestFactory.Order();
            var item = TestFactory.OrderItem("oi1", "m1", price: 50, qty: 1);
            vm.OrderItems.Add(item);
            vm.RecalcTotal(); // 50

            os.Setup(x => x.RemoveOrderItemAsync("oi1"))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));
            os.Setup(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await vm.RemoveItemAsync(item);

            vm.OrderItems.Should().BeEmpty();
            vm.Total.Should().Be(0);
        }

        [Fact(DisplayName = "Remove: Son ürün silinince sipariş otomatik kapatılır")]
        public async Task RemoveItemAsync_LastItem_OrderAutoClosed()
        {
            var (vm, _, os, _) = BuildVm();
            var order = TestFactory.Order("o1", "t1");
            vm.CurrentOrder = order;
            vm.TableId = "t1";
            var item = TestFactory.OrderItem("oi1", "m1", price: 50, qty: 1);
            vm.OrderItems.Add(item);
            vm.RecalcTotal(); // 50

            os.Setup(x => x.RemoveOrderItemAsync("oi1"))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));
            os.Setup(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await vm.RemoveItemAsync(item);

            os.Verify(x => x.CloseOrderAsync(It.Is<CloseOrderRequest>(r =>
                r.OrderId == "o1" && r.TableId == "t1")), Times.Once);
            vm.CurrentOrder.Should().BeNull();
        }

        // ─── CloseOrderAsync ──────────────────────────────────────────────

        [Fact(DisplayName = "CloseOrder: CurrentOrder null iken hiçbir şey yapmaz")]
        public async Task CloseOrderAsync_NullCurrentOrder_DoesNothing()
        {
            var (vm, _, os, _) = BuildVm();
            vm.CurrentOrder = null;

            await vm.CloseOrderAsync();

            os.Verify(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()), Times.Never);
        }

        [Fact(DisplayName = "CloseOrder: Doğru FinalTotal ile servis çağrılır")]
        public async Task CloseOrderAsync_CallsServiceWithCorrectTotal()
        {
            var (vm, _, os, _) = BuildVm();
            vm.CurrentOrder = TestFactory.Order("o1", "t1");
            vm.TableId = "t1";
            vm.OrderItems.Add(TestFactory.OrderItem("oi1", "m1", price: 80, qty: 2));
            vm.OrderItems.Add(TestFactory.OrderItem("oi2", "m2", price: 30, qty: 1));
            vm.RecalcTotal(); // 190

            os.Setup(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await vm.CloseOrderAsync();

            os.Verify(x => x.CloseOrderAsync(It.Is<CloseOrderRequest>(r =>
                r.FinalTotal == 190 && r.OrderId == "o1")), Times.Once);
            vm.CurrentOrder.Should().BeNull();
            vm.Total.Should().Be(0);
            vm.OrderItems.Should().BeEmpty();
        }

        // ─── RecalcTotal ──────────────────────────────────────────────────

        [Fact(DisplayName = "RecalcTotal: Boş sepette toplam sıfır")]
        public void RecalcTotal_EmptyCart_Zero()
        {
            var (vm, _, _, _) = BuildVm();
            vm.RecalcTotal();
            vm.Total.Should().Be(0);
        }

        [Fact(DisplayName = "RecalcTotal: Çoklu ürün doğru toplamı verir")]
        public void RecalcTotal_MultipleItems_CorrectTotal()
        {
            var (vm, _, _, _) = BuildVm();
            vm.OrderItems.Add(TestFactory.OrderItem("i1", "m1", price: 45, qty: 2));
            vm.OrderItems.Add(TestFactory.OrderItem("i2", "m2", price: 60, qty: 3));
            vm.OrderItems.Add(TestFactory.OrderItem("i3", "m3", price: 10, qty: 1));

            vm.RecalcTotal();

            vm.Total.Should().Be(280); // 90 + 180 + 10
        }

        [Fact(DisplayName = "RecalcTotal: Adet değişiminden sonra doğru güncellenir")]
        public void RecalcTotal_AfterQtyChange_UpdatesCorrectly()
        {
            var (vm, _, _, _) = BuildVm();
            var item = TestFactory.OrderItem("i1", "m1", price: 50, qty: 2);
            vm.OrderItems.Add(item);
            vm.RecalcTotal();
            vm.Total.Should().Be(100);

            item.Quantity = 5;
            vm.RecalcTotal();
            vm.Total.Should().Be(250);
        }

        // ─── EnsureOrderCreated ───────────────────────────────────────────

        [Fact(DisplayName = "EnsureOrder: Zaten sipariş varsa yeni oluşturmaz")]
        public async Task EnsureOrderCreatedAsync_AlreadyExists_NoNewOrder()
        {
            var (vm, _, os, _) = BuildVm();
            vm.CurrentOrder = TestFactory.Order();

            await vm.EnsureOrderCreatedAsync("t1");

            os.Verify(x => x.CreateOrderAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact(DisplayName = "EnsureOrder: Sipariş yoksa oluşturulur ve masa dolu yapılır")]
        public async Task EnsureOrderCreatedAsync_NoOrder_CreatesAndSetsTableDolu()
        {
            var (vm, _, os, ts) = BuildVm();
            vm.CurrentOrder = null;
            var newOrder = TestFactory.Order("o1", "t1");

            os.Setup(x => x.CreateOrderAsync("t1"))
              .ReturnsAsync(BaseResponse<OrderModel>.SuccessResult(newOrder));
            ts.Setup(x => x.UpdateTableStatusAsync(It.IsAny<UpdateTableStatusRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await vm.EnsureOrderCreatedAsync("t1");

            vm.CurrentOrder.Should().NotBeNull();
            ts.Verify(x => x.UpdateTableStatusAsync(It.Is<UpdateTableStatusRequest>(r =>
                r.TableId == "t1" && r.Status == "dolu")), Times.Once);
        }

        // ─── UÇ DURUMLAR ──────────────────────────────────────────────────

        [Fact(DisplayName = "UÇ: Servis hata dönünce StatusMessage set edilir, exception yok")]
        public async Task EdgeCase_ServiceError_SetsStatusMessageNoException()
        {
            var (vm, _, os, _) = BuildVm();
            var order = TestFactory.Order("o1", "t1");
            vm.CurrentOrder = order;
            vm.TableId = "t1";

            os.Setup(x => x.CloseOrderAsync(It.IsAny<CloseOrderRequest>()))
              .ReturnsAsync(BaseResponse<object>.ErrorResult("DB bağlantı hatası"));

            var act = async () => await vm.CloseOrderAsync();
            await act.Should().NotThrowAsync("hata mesajı exception değil StatusMessage ile iletilmeli");
            vm.StatusMessage.Should().Be("DB bağlantı hatası");
        }

        [Fact(DisplayName = "UÇ: Eşzamanlı EnsureOrderCreated — çift sipariş açılmaz (SemaphoreSlim)")]
        public async Task EdgeCase_ConcurrentEnsureOrder_OnlyOneOrderCreated()
        {
            var (vm, _, os, ts) = BuildVm();
            vm.CurrentOrder = null;
            var callCount = 0;
            var newOrder = TestFactory.Order("o1", "t1");

            os.Setup(x => x.CreateOrderAsync("t1"))
              .ReturnsAsync(() =>
              {
                  callCount++;
                  return BaseResponse<OrderModel>.SuccessResult(newOrder);
              });
            ts.Setup(x => x.UpdateTableStatusAsync(It.IsAny<UpdateTableStatusRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => vm.EnsureOrderCreatedAsync("t1"))
                .ToArray();
            await Task.WhenAll(tasks);

            callCount.Should().Be(1, "aynı anda 5 çağrı yapılsa bile sipariş bir kez açılmalı");
        }

        [Fact(DisplayName = "UÇ: SetItemQuantityAsync ile adet güncellenir")]
        public async Task SetItemQuantityAsync_UpdatesQuantityAndCallsService()
        {
            var (vm, _, os, _) = BuildVm();
            var item = TestFactory.OrderItem("oi1", "m1", price: 50, qty: 3);

            os.Setup(x => x.UpdateOrderItemQuantityAsync(It.IsAny<UpdateOrderItemQuantityRequest>()))
              .ReturnsAsync(BaseResponse<object>.SuccessResult(null));

            await vm.SetItemQuantityAsync(item, 2);

            item.Quantity.Should().Be(2);
            os.Verify(x => x.UpdateOrderItemQuantityAsync(It.Is<UpdateOrderItemQuantityRequest>(r =>
                r.ItemId == "oi1" && r.Quantity == 2)), Times.Once);
        }
    }
}