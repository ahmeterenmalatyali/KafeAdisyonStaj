using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.IntegrationTests.Infrastructure;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace KafeAdisyon.IntegrationTests.Tests.Performance
{
    [Collection("Database")]
    public class PerformanceTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fx;
        private readonly ITestOutputHelper _out;

        public PerformanceTests(DatabaseFixture fx, ITestOutputHelper output)
        {
            _fx = fx;
            _out = output;
        }

        private static long Measure(Stopwatch sw) { sw.Stop(); return sw.ElapsedMilliseconds; }

        // ─── TEK İŞLEM HIZI ──────────────────────────────────────────

        [Fact(DisplayName = "PERF | Menü listesi: 3 saniyenin altında gelmeli")]
        public async Task GetMenuItems_UnderThreshold()
        {
            var sw = Stopwatch.StartNew();
            var response = await _fx.MenuService.GetAllMenuItemsAsync();
            long ms = Measure(sw);

            _out.WriteLine($"GetAllMenuItems → {ms}ms | {response.Data?.Count ?? 0} ürün");

            response.Success.Should().BeTrue(response.Message);
            ms.Should().BeLessThan(_fx.Settings.GetMenuItemsMs,
                $"menü listesi {_fx.Settings.GetMenuItemsMs}ms limitinin altında gelmeli, gerçek: {ms}ms");
        }

        [Fact(DisplayName = "PERF | Masa listesi: 3 saniyenin altında gelmeli")]
        public async Task GetTables_UnderThreshold()
        {
            var sw = Stopwatch.StartNew();
            var response = await _fx.TableService.GetAllTablesAsync();
            long ms = Measure(sw);

            _out.WriteLine($"GetAllTables → {ms}ms | {response.Data?.Count ?? 0} masa");

            response.Success.Should().BeTrue(response.Message);
            ms.Should().BeLessThan(_fx.Settings.GetTablesMs,
                $"masa listesi {_fx.Settings.GetTablesMs}ms limitinin altında gelmeli, gerçek: {ms}ms");
        }

        [Fact(DisplayName = "PERF | Sipariş oluşturma: 3 saniyenin altında olmalı")]
        public async Task CreateOrder_UnderThreshold()
        {
            var table = (await _fx.TableService.GetAllTablesAsync()).Data!.First();

            // Mevcut aktifi temizle
            var ex = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (ex.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                    { OrderId = ex.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var sw = Stopwatch.StartNew();
            var response = await _fx.OrderService.CreateOrderAsync(table.Id);
            long ms = Measure(sw);

            _out.WriteLine($"CreateOrder → {ms}ms | OrderId: {response.Data?.Id}");
            _fx.TrackOrder(response.Data?.Id ?? "");

            response.Success.Should().BeTrue(response.Message);
            ms.Should().BeLessThan(_fx.Settings.CreateOrderMs,
                $"sipariş oluşturma {_fx.Settings.CreateOrderMs}ms limitinin altında olmalı, gerçek: {ms}ms");
        }

        [Fact(DisplayName = "PERF | Ürün ekleme: 3 saniyenin altında olmalı")]
        public async Task AddOrderItem_UnderThreshold()
        {
            var table = (await _fx.TableService.GetAllTablesAsync()).Data!.First();
            var menuItem = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!.First();

            var ex = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (ex.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                    { OrderId = ex.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            var sw = Stopwatch.StartNew();
            var response = await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = 1,
                Price = menuItem.Price
            });
            long ms = Measure(sw);

            _out.WriteLine($"AddOrderItem → {ms}ms | ItemId: {response.Data?.Id}");

            response.Success.Should().BeTrue(response.Message);
            ms.Should().BeLessThan(_fx.Settings.AddOrderItemMs,
                $"ürün ekleme {_fx.Settings.AddOrderItemMs}ms limitinin altında olmalı, gerçek: {ms}ms");
        }

        [Fact(DisplayName = "PERF | Hesap kapatma: 3 saniyenin altında olmalı")]
        public async Task CloseOrder_UnderThreshold()
        {
            var table = (await _fx.TableService.GetAllTablesAsync()).Data!.First();
            var menuItem = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!.First();

            var ex = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (ex.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                    { OrderId = ex.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = order.Id, MenuItemId = menuItem.Id, Quantity = 1, Price = menuItem.Price });

            var sw = Stopwatch.StartNew();
            var response = await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = order.Id, TableId = table.Id, FinalTotal = menuItem.Price });
            long ms = Measure(sw);

            _out.WriteLine($"CloseOrder → {ms}ms");

            response.Success.Should().BeTrue(response.Message);
            ms.Should().BeLessThan(_fx.Settings.CloseOrderMs,
                $"hesap kapatma {_fx.Settings.CloseOrderMs}ms limitinin altında olmalı, gerçek: {ms}ms");
        }

        // ─── TAM SENARYO HIZI ─────────────────────────────────────────

        [Fact(DisplayName = "PERF | Tam sipariş akışı: 10 saniyenin altında tamamlanmalı")]
        public async Task FullOrderFlow_UnderThreshold()
        {
            var table = (await _fx.TableService.GetAllTablesAsync()).Data!.First();
            var menuItems = (await _fx.MenuService.GetAllMenuItemsAsync()).Data!;
            menuItems.Should().HaveCountGreaterThanOrEqualTo(2);

            var ex = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (ex.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                    { OrderId = ex.Data.Id, TableId = table.Id, FinalTotal = 0 });

            var timings = new Dictionary<string, long>();
            var totalSw = Stopwatch.StartNew();

            // 1. Sipariş aç
            var sw = Stopwatch.StartNew();
            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            timings["CreateOrder"] = Measure(sw);
            _fx.TrackOrder(order.Id);

            // 2. Masa dolu
            sw.Restart();
            await _fx.TableService.UpdateTableStatusAsync(new UpdateTableStatusRequest
                { TableId = table.Id, Status = "dolu" });
            timings["UpdateTableDolu"] = Measure(sw);

            // 3. Birinci ürün ekle
            sw.Restart();
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = order.Id, MenuItemId = menuItems[0].Id, Quantity = 2, Price = menuItems[0].Price });
            timings["AddItem1"] = Measure(sw);

            // 4. İkinci ürün ekle
            sw.Restart();
            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
                { OrderId = order.Id, MenuItemId = menuItems[1].Id, Quantity = 1, Price = menuItems[1].Price });
            timings["AddItem2"] = Measure(sw);

            // 5. Kalemleri oku
            sw.Restart();
            var items = await _fx.OrderService.GetOrderItemsAsync(order.Id);
            timings["GetItems"] = Measure(sw);

            double total = items.Data!.Sum(i => i.Price * i.Quantity);

            // 6. Hesabı kapat
            sw.Restart();
            await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = order.Id, TableId = table.Id, FinalTotal = total });
            timings["CloseOrder"] = Measure(sw);

            totalSw.Stop();
            long totalMs = totalSw.ElapsedMilliseconds;

            // Rapor
            _out.WriteLine("─── Adım Adım Süreler ───────────────────────────────");
            foreach (var (step, ms) in timings)
                _out.WriteLine($"  {step,-22} → {ms,5}ms");
            _out.WriteLine($"  {"TOPLAM",-22} → {totalMs,5}ms");
            _out.WriteLine("─────────────────────────────────────────────────────");

            totalMs.Should().BeLessThan(_fx.Settings.FullOrderFlowMs,
                $"tam sipariş akışı {_fx.Settings.FullOrderFlowMs}ms limitinin altında olmalı, gerçek: {totalMs}ms");
        }

        // ─── TEKRARLANABILIRLIK ───────────────────────────────────────

        [Fact(DisplayName = "PERF | Menü listesi 5 kez arka arkaya — ortalama tutarlı")]
        public async Task GetMenuItems_RepeatedCalls_ConsistentPerformance()
        {
            const int runs = 5;
            var times = new List<long>();

            for (int i = 0; i < runs; i++)
            {
                var sw = Stopwatch.StartNew();
                await _fx.MenuService.GetAllMenuItemsAsync();
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);
                await Task.Delay(200); // rate limit koruma
            }

            double avg = times.Average();
            long max = times.Max();
            long min = times.Min();

            _out.WriteLine($"5x GetMenuItems: min={min}ms avg={avg:F0}ms max={max}ms");
            _out.WriteLine($"Değerler: {string.Join(", ", times.Select(t => t + "ms"))}");

            max.Should().BeLessThan(_fx.Settings.GetMenuItemsMs,
                $"hiçbir tekrar {_fx.Settings.GetMenuItemsMs}ms sınırını aşmamalı, max: {max}ms");

            // İlk çağrı (soğuk başlangıç) dışında varyans düşük olmalı
            var withoutFirst = times.Skip(1).ToList();
            double variance = withoutFirst.Max() - withoutFirst.Min();
            _out.WriteLine($"2-5. çağrı varyansı: {variance}ms");
            variance.Should().BeLessThan(2000,
                "tekrarlayan çağrılarda süre varyansı 2 saniyenin altında olmalı");
        }
    }
}
