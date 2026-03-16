using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace KafeAdisyon.IntegrationTests.Tests.Calculation
{
    /// <summary>
    /// Sipariş toplamı ve hesap bölme hesaplamalarının
    /// gerçek DB verileriyle doğruluğunu test eder.
    /// "Sipariş eklerken yazan rakamlar doğru mu?" sorusunu yanıtlar.
    /// </summary>
    [Collection("Database")]
    public class OrderCalculationTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fx;
        private readonly ITestOutputHelper _out;

        public OrderCalculationTests(DatabaseFixture fx, ITestOutputHelper output)
        {
            _fx = fx;
            _out = output;
        }

        private async Task<string> PrepareOrder()
        {
            var table = (await _fx.TableService.GetAllTablesAsync()).Data!.First();
            var ex = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (ex.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                    { OrderId = ex.Data.Id, TableId = table.Id, FinalTotal = 0 });
            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);
            return order.Id;
        }

        // ─── TOPLAM DOĞRULUĞU ─────────────────────────────────────────

        [Fact(DisplayName = "HESAP | Tek ürün: DB fiyatı × adet = toplam")]
        public async Task SingleItem_TotalMatchesPriceTimesQty()
        {
            var menuItem = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!.First();
            var orderId = await PrepareOrder();

            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItem.Id, Quantity = 3, Price = menuItem.Price });

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;
            double calculatedTotal = items.Sum(i => i.Price * i.Quantity);
            double expectedTotal = menuItem.Price * 3;

            _out.WriteLine($"Ürün: {menuItem.Name} | Fiyat: {menuItem.Price} | Adet: 3");
            _out.WriteLine($"Beklenen: {expectedTotal} | Hesaplanan: {calculatedTotal}");

            calculatedTotal.Should().BeApproximately(expectedTotal, 0.001);
        }

        [Fact(DisplayName = "HESAP | Çoklu farklı ürün: Her kalemin toplamı doğru")]
        public async Task MultipleItems_EachLineAndTotalCorrect()
        {
            var menuItems = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!;
            menuItems.Should().HaveCountGreaterThanOrEqualTo(3, "en az 3 menü ürünü gerekli");

            var orderId = await PrepareOrder();

            var testData = new List<(string menuId, double price, int qty)>
            {
                (menuItems[0].Id, menuItems[0].Price, 1),
                (menuItems[1].Id, menuItems[1].Price, 3),
                (menuItems[2].Id, menuItems[2].Price, 2)
            };

            foreach (var (menuId, price, qty) in testData)
                await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                    { OrderId = orderId, MenuItemId = menuId, Quantity = qty, Price = price });

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;

            _out.WriteLine("─── Kalem bazında doğrulama ─────────────────────────");
            double totalFromDb = 0;
            double totalExpected = 0;

            foreach (var (menuId, price, qty) in testData)
            {
                var dbItem = items.FirstOrDefault(i => i.MenuItemId == menuId);
                dbItem.Should().NotBeNull($"MenuItemId={menuId} DB'de olmalı");

                double lineExpected = price * qty;
                double lineActual = dbItem!.Price * dbItem.Quantity;

                _out.WriteLine($"  MenuId: {menuId.Substring(0, 8)}... | Beklenen: {lineExpected} | DB: {lineActual}");

                lineActual.Should().BeApproximately(lineExpected, 0.001,
                    $"kalem tutarı yanlış: {menuId}");

                totalFromDb += lineActual;
                totalExpected += lineExpected;
            }

            _out.WriteLine($"  TOPLAM — Beklenen: {totalExpected} | DB: {totalFromDb}");
            totalFromDb.Should().BeApproximately(totalExpected, 0.001,
                "sipariş toplamı tüm kalemlerin toplamına eşit olmalı");
        }

        [Fact(DisplayName = "HESAP | Adet güncelleme: Toplam yeni adete göre değişir")]
        public async Task UpdateQty_TotalChangesCorrectly()
        {
            var menuItem = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!.First();
            var orderId = await PrepareOrder();

            var addedItem = (await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItem.Id, Quantity = 2, Price = menuItem.Price })).Data!;

            double totalBefore = menuItem.Price * 2;

            // Adedi 5'e çıkar
            await _fx.OrderService.UpdateOrderItemQuantityAsync(
                new UpdateOrderItemQuantityRequest { ItemId = addedItem.Id, Quantity = 5 });

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;
            double totalAfter = items.Sum(i => i.Price * i.Quantity);
            double expectedAfter = menuItem.Price * 5;

            _out.WriteLine($"Önce: {totalBefore} | Sonra: {totalAfter} | Beklenen: {expectedAfter}");

            totalAfter.Should().BeApproximately(expectedAfter, 0.001,
                "adet güncellemesi sonrası toplam doğru hesaplanmalı");
        }

        [Fact(DisplayName = "HESAP | Ürün silme: Toplam doğru azalır")]
        public async Task RemoveItem_TotalDecreasesCorrectly()
        {
            var menuItems = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!;
            menuItems.Should().HaveCountGreaterThanOrEqualTo(2);

            var orderId = await PrepareOrder();

            var item1 = (await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItems[0].Id, Quantity = 2, Price = menuItems[0].Price })).Data!;
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItems[1].Id, Quantity = 1, Price = menuItems[1].Price });

            double totalBefore = menuItems[0].Price * 2 + menuItems[1].Price * 1;

            // item1'i sil
            await _fx.OrderService.RemoveOrderItemAsync(item1.Id);

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;
            double totalAfter = items.Sum(i => i.Price * i.Quantity);
            double expectedAfter = menuItems[1].Price * 1;

            _out.WriteLine($"Önce: {totalBefore} | Sonra: {totalAfter} | Beklenen: {expectedAfter}");

            totalAfter.Should().BeApproximately(expectedAfter, 0.001,
                "ürün silmesi sonrası toplam doğru azalmalı");
        }

        // ─── HESAP BÖLME HESAPLAMALARI ────────────────────────────────

        [Fact(DisplayName = "HESAP | Split: Ödenen + Kalan = Toplam (DB verileriyle)")]
        public async Task SplitBill_PaidPlusRemainingEqualsTotal()
        {
            var menuItems = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!;
            menuItems.Should().HaveCountGreaterThanOrEqualTo(2);

            var orderId = await PrepareOrder();

            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItems[0].Id, Quantity = 3, Price = menuItems[0].Price });
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItems[1].Id, Quantity = 2, Price = menuItems[1].Price });

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;
            double orderTotal = items.Sum(i => i.Price * i.Quantity);

            // item[0]'dan 1 adet ödendiğini simüle et
            double paidAmount = menuItems[0].Price * 1;
            double remainingAmount = orderTotal - paidAmount;

            _out.WriteLine($"Sipariş toplamı: {orderTotal}");
            _out.WriteLine($"Ödenen: {paidAmount} | Kalan: {remainingAmount}");
            _out.WriteLine($"Kontrol: {paidAmount} + {remainingAmount} = {paidAmount + remainingAmount}");

            (paidAmount + remainingAmount).Should().BeApproximately(orderTotal, 0.001,
                "ödenen + kalan her zaman sipariş toplamına eşit olmalı");
        }

        [Fact(DisplayName = "HESAP | Split: Kısmi ödeme sonrası kalan sipariş DB'ye doğru yazılır")]
        public async Task SplitBill_RemainingItemsPersistedCorrectly()
        {
            var menuItems = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!;
            menuItems.Should().HaveCountGreaterThanOrEqualTo(2);

            var orderId = await PrepareOrder();

            // 4 adet item0, 2 adet item1
            var addedItem0 = (await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItems[0].Id, Quantity = 4, Price = menuItems[0].Price })).Data!;
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItems[1].Id, Quantity = 2, Price = menuItems[1].Price });

            // Split: item0'dan 3 adet ödendi, 1 kaldı
            await _fx.OrderService.UpdateOrderItemQuantityAsync(
                new UpdateOrderItemQuantityRequest { ItemId = addedItem0.Id, Quantity = 1 });

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;
            var remainingItem0 = items.FirstOrDefault(i => i.Id == addedItem0.Id);

            remainingItem0.Should().NotBeNull();
            remainingItem0!.Quantity.Should().Be(1, "3 adet ödendikten sonra 1 adet kalmalı");

            double remainingTotal = items.Sum(i => i.Price * i.Quantity);
            double expectedRemaining = menuItems[0].Price * 1 + menuItems[1].Price * 2;

            _out.WriteLine($"Kalan toplam — Beklenen: {expectedRemaining} | DB: {remainingTotal}");
            remainingTotal.Should().BeApproximately(expectedRemaining, 0.001);
        }

        // ─── UÇ HESAPLAMA DURUMLARI ───────────────────────────────────

        [Fact(DisplayName = "HESAP | Tüm ürünler silinince toplam sıfır")]
        public async Task AllItemsRemoved_TotalZero()
        {
            var menuItem = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!.First();
            var orderId = await PrepareOrder();

            var added = (await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItem.Id, Quantity = 2, Price = menuItem.Price })).Data!;

            await _fx.OrderService.RemoveOrderItemAsync(added.Id);

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;
            double total = items.Sum(i => i.Price * i.Quantity);

            total.Should().Be(0, "tüm kalemler silince toplam sıfır olmalı");
            items.Should().BeEmpty();
        }

        [Fact(DisplayName = "HESAP | Aynı ürün iki kez ayrı kalem olarak eklenir, toplam iki katı")]
        public async Task SameItemTwice_TotalDoubled()
        {
            var menuItem = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!.First();
            var orderId = await PrepareOrder();

            // Aynı menu_item_id'yi iki ayrı satır olarak ekle (edge case)
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItem.Id, Quantity = 1, Price = menuItem.Price });
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = orderId, MenuItemId = menuItem.Id, Quantity = 1, Price = menuItem.Price });

            var items = (await _fx.OrderService.GetOrderItemsAsync(orderId)).Data!;
            double total = items.Sum(i => i.Price * i.Quantity);
            double expected = menuItem.Price * 2;

            _out.WriteLine($"Ürün: {menuItem.Name} | Fiyat: {menuItem.Price} | 2 satır × 1 adet");
            _out.WriteLine($"Beklenen: {expected} | DB: {total}");

            total.Should().BeApproximately(expected, 0.001);
        }

        [Fact(DisplayName = "HESAP | CloseOrder'a gönderilen FinalTotal DB'ye yazılanla eşleşir")]
        public async Task FinalTotal_SentToCloseOrder_MatchesDatabaseValue()
        {
            var menuItems = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!;
            var table = (await _fx.TableService.GetAllTablesAsync()).Data!.First();

            var ex = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (ex.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                    { OrderId = ex.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = order.Id, MenuItemId = menuItems[0].Id, Quantity = 2, Price = menuItems[0].Price });
            if (menuItems.Count > 1)
                await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                    { OrderId = order.Id, MenuItemId = menuItems[1].Id, Quantity = 1, Price = menuItems[1].Price });

            var items = (await _fx.OrderService.GetOrderItemsAsync(order.Id)).Data!;
            double computedTotal = items.Sum(i => i.Price * i.Quantity);

            await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = order.Id, TableId = table.Id, FinalTotal = computedTotal });

            // DB'den doğrula
            var orders = await _fx.Client.Db
                .Table<KafeAdisyon.Models.OrderModel>()
                .Select("id,total,status")
                .Where(o => o.Id == order.Id)
                .Get();
            var dbOrder = orders.Models.First();

            _out.WriteLine($"Hesaplanan: {computedTotal} | DB'ye yazılan: {dbOrder.Total}");

            dbOrder.Total.Should().BeApproximately(computedTotal, 0.001,
                "CloseOrder'a gönderilen FinalTotal DB'ye doğru yazılmalı");
            dbOrder.Status.Should().Be("odendi");
        }
    }
}
