using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.IntegrationTests.Infrastructure;
using KafeAdisyon.Models;
using Xunit;

namespace KafeAdisyon.IntegrationTests.Tests.Integration
{
    [Collection("Database")]
    public class OrderServiceIntegrationTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fx;
        public OrderServiceIntegrationTests(DatabaseFixture fx) => _fx = fx;

        private async Task<TableModel> GetAnyTable()
        {
            var r = await _fx.TableService.GetAllTablesAsync();
            r.Success.Should().BeTrue(r.Message);
            r.Data.Should().NotBeEmpty("tablolar DB'de kayıtlı olmalı");
            return r.Data!.First();
        }

        private async Task<MenuItemModel> GetAnyMenuItem()
        {
            var r = await _fx.MenuService.GetAllMenuItemsAsync();
            r.Success.Should().BeTrue(r.Message);
            r.Data.Should().NotBeEmpty("en az 1 aktif menü ürünü olmalı");
            return r.Data!.First();
        }

        // ─── TAM SENARYO ─────────────────────────────────────────────

        [Fact(DisplayName = "DB | Sipariş: Tam akış — aç → ürün ekle → kapat → masa boş")]
        public async Task FullOrderFlow_CreateAddItemClose_TableBecomesAvailable()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();

            // 0. Tabloda önceki çalışmadan kalan aktif sipariş varsa temizle
            //    GetAnyTable() her seferinde aynı tabloyu (.First()) seçer;
            //    önceki test çalışması cleanup yapmadan bitmişse sahte bir aktif
            //    sipariş kalıyor ve testin sonundaki kontrol yanıltıcı sonuç veriyor.
            var staleOrder = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (staleOrder.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = staleOrder.Data.Id, TableId = table.Id, FinalTotal = 0 });

            // 1. Siparişi aç
            var createResp = await _fx.OrderService.CreateOrderAsync(table.Id);
            createResp.Success.Should().BeTrue(createResp.Message);
            var order = createResp.Data!;
            _fx.TrackOrder(order.Id);

            order.Id.Should().NotBeNullOrEmpty("DB UUID atamalı");
            order.Status.Should().Be("aktif");
            order.TableId.Should().Be(table.Id);

            // 2. Masayı dolu yap
            var tableUpdateResp = await _fx.TableService.UpdateTableStatusAsync(
                new UpdateTableStatusRequest { TableId = table.Id, Status = "dolu" });
            tableUpdateResp.Success.Should().BeTrue(tableUpdateResp.Message);

            // 3. Ürün ekle
            var addItemResp = await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = 2,
                Price = menuItem.Price
            });
            addItemResp.Success.Should().BeTrue(addItemResp.Message);
            addItemResp.Data!.Quantity.Should().Be(2);
            addItemResp.Data.Price.Should().Be(menuItem.Price);

            // 4. Sipariş kalemlerini oku — 1 kalem olmalı
            var itemsResp = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            itemsResp.Success.Should().BeTrue(itemsResp.Message);
            itemsResp.Data!.Should().HaveCount(1);
            itemsResp.Data[0].Quantity.Should().Be(2);

            // 5. Toplam hesapla
            double expectedTotal = menuItem.Price * 2;

            // 6. Siparişi kapat
            var closeResp = await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
            {
                OrderId = order.Id,
                TableId = table.Id,
                FinalTotal = expectedTotal
            });
            closeResp.Success.Should().BeTrue(closeResp.Message);

            // 7. Masa durumu bos olmalı
            var tablesResp = await _fx.TableService.GetAllTablesAsync();
            var updatedTable = tablesResp.Data!.FirstOrDefault(t => t.Id == table.Id);
            updatedTable.Should().NotBeNull();
            updatedTable!.Status.Should().Be("bos", "hesap kapanınca masa serbest kalmalı");

            // 8. Aktif sipariş yoktur
            var activeOrderResp = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            activeOrderResp.Success.Should().BeTrue(activeOrderResp.Message);
            activeOrderResp.Data.Should().BeNull("kapatılan sipariş artık aktif olmamalı");
        }

        [Fact(DisplayName = "DB | Sipariş: Aynı masada aynı anda sadece 1 aktif sipariş olur")]
        public async Task GetActiveOrder_AfterCreate_ReturnsCorrectOrder()
        {
            var table = await GetAnyTable();

            // Önce mevcut aktif siparişleri temizle
            var existing = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (existing.Data != null)
            {
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = existing.Data.Id, TableId = table.Id, FinalTotal = 0 });
            }

            var createResp = await _fx.OrderService.CreateOrderAsync(table.Id);
            createResp.Success.Should().BeTrue(createResp.Message);
            _fx.TrackOrder(createResp.Data!.Id);

            var activeResp = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            activeResp.Success.Should().BeTrue(activeResp.Message);
            activeResp.Data.Should().NotBeNull();
            activeResp.Data!.Id.Should().Be(createResp.Data.Id);
        }

        [Fact(DisplayName = "DB | Sipariş: Çoklu ürün eklenir, toplam doğru hesaplanır")]
        public async Task MultipleItems_TotalCalculationCorrect()
        {
            var table = await GetAnyTable();
            var menuItems = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!;
            menuItems.Should().HaveCountGreaterThanOrEqualTo(2, "en az 2 menü ürünü gerekli");

            var item1 = menuItems[0];
            var item2 = menuItems[1];

            // Temiz masa — mevcut aktifi kapat
            var existing = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (existing.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = existing.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var createResp = await _fx.OrderService.CreateOrderAsync(table.Id);
            createResp.Success.Should().BeTrue(createResp.Message);
            var order = createResp.Data!;
            _fx.TrackOrder(order.Id);

            // Item1: 3 adet
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            { OrderId = order.Id, MenuItemId = item1.Id, Quantity = 3, Price = item1.Price });

            // Item2: 2 adet
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            { OrderId = order.Id, MenuItemId = item2.Id, Quantity = 2, Price = item2.Price });

            var itemsResp = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            itemsResp.Success.Should().BeTrue(itemsResp.Message);
            itemsResp.Data!.Should().HaveCount(2);

            double dbTotal = itemsResp.Data.Sum(i => i.Price * i.Quantity);
            double expectedTotal = item1.Price * 3 + item2.Price * 2;

            dbTotal.Should().BeApproximately(expectedTotal, 0.001,
                "DB'den okunan fiyat × adet toplamı beklenenle eşleşmeli");
        }

        [Fact(DisplayName = "DB | Sipariş: Adet güncelleme DB'ye yansır")]
        public async Task UpdateOrderItemQuantity_PersistsToDatabase()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();

            var existing = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (existing.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = existing.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            var addedItem = (await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            { OrderId = order.Id, MenuItemId = menuItem.Id, Quantity = 1, Price = menuItem.Price })).Data!;

            // Adedi 4'e çıkar
            var updateResp = await _fx.OrderService.UpdateOrderItemQuantityAsync(
                new UpdateOrderItemQuantityRequest { ItemId = addedItem.Id, Quantity = 4 });
            updateResp.Success.Should().BeTrue(updateResp.Message);

            // DB'den oku — 4 olmalı
            var itemsResp = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            var updated = itemsResp.Data!.FirstOrDefault(i => i.Id == addedItem.Id);
            updated.Should().NotBeNull();
            updated!.Quantity.Should().Be(4, "güncelleme DB'ye yansımalı");
        }

        [Fact(DisplayName = "DB | Sipariş: Ürün silme DB'den kaldırır")]
        public async Task RemoveOrderItem_RemovesFromDatabase()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();

            var existing = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (existing.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = existing.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            var addedItem = (await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            { OrderId = order.Id, MenuItemId = menuItem.Id, Quantity = 2, Price = menuItem.Price })).Data!;

            var removeResp = await _fx.OrderService.RemoveOrderItemAsync(addedItem.Id);
            removeResp.Success.Should().BeTrue(removeResp.Message);

            var itemsResp = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            itemsResp.Data!.Should().NotContain(i => i.Id == addedItem.Id,
                "silinen kalem listede görünmemeli");
        }

        [Fact(DisplayName = "DB | Sipariş: FinalTotal kapatma sonrası DB'ye yazılır")]
        public async Task CloseOrder_FinalTotalPersistedCorrectly()
        {
            var table = await GetAnyTable();
            var menuItem = await GetAnyMenuItem();

            var existing = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (existing.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = existing.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            { OrderId = order.Id, MenuItemId = menuItem.Id, Quantity = 3, Price = menuItem.Price });

            double finalTotal = menuItem.Price * 3;

            await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
            { OrderId = order.Id, TableId = table.Id, FinalTotal = finalTotal });

            // DB'den siparişi oku — total ve status kontrol
            var orders = await _fx.Client.Db
                .Table<OrderModel>()
                .Select("id,table_id,status,total")
                .Where(o => o.Id == order.Id)
                .Get();
            var closed = orders.Models.FirstOrDefault();
            closed.Should().NotBeNull();
            closed!.Status.Should().Be("odendi");
            closed.Total.Should().BeApproximately(finalTotal, 0.001,
                "kapatma toplam tutarı DB'ye doğru yazılmalı");
        }
    }
}