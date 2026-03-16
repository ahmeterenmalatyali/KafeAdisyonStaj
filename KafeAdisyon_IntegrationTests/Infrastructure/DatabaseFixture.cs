using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Infrastructure.Services;
using KafeAdisyon.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KafeAdisyon.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Gerçek Supabase veritabanına bağlanan fixture.
    /// xUnit'in IClassFixture ile testler arasında paylaşılır — bağlantı bir kez kurulur.
    /// Test sonrası oluşturulan tüm veriler temizlenir (Cleanup).
    /// </summary>
    public class DatabaseFixture : IAsyncLifetime
    {
        public IMenuService MenuService { get; private set; } = null!;
        public ITableService TableService { get; private set; } = null!;
        public IOrderService OrderService { get; private set; } = null!;
        public DatabaseClient Client { get; private set; } = null!;
        public IConfiguration Config { get; private set; } = null!;
        public TestSettings Settings { get; private set; } = null!;

        // Test sırasında açılan siparişlerin ID'lerini tutar — cleanup için
        private readonly List<string> _testOrderIds = new();
        private readonly List<string> _testMenuItemIds = new();

        public void TrackOrder(string orderId) => _testOrderIds.Add(orderId);
        public void TrackMenuItem(string itemId) => _testMenuItemIds.Add(itemId);

        public async Task InitializeAsync()
        {
            Config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.test.json", optional: false)
                .Build();

            Client = new DatabaseClient(Config);
            TableService = new TableService(Client);
            MenuService = new MenuService(Client);
            OrderService = new OrderService(Client, TableService);

            Settings = new TestSettings
            {
                GetMenuItemsMs = int.Parse(Config["TestSettings:PerformanceThresholds:GetMenuItemsMs"]!),
                GetTablesMs = int.Parse(Config["TestSettings:PerformanceThresholds:GetTablesMs"]!),
                CreateOrderMs = int.Parse(Config["TestSettings:PerformanceThresholds:CreateOrderMs"]!),
                AddOrderItemMs = int.Parse(Config["TestSettings:PerformanceThresholds:AddOrderItemMs"]!),
                CloseOrderMs = int.Parse(Config["TestSettings:PerformanceThresholds:CloseOrderMs"]!),
                FullOrderFlowMs = int.Parse(Config["TestSettings:PerformanceThresholds:FullOrderFlowMs"]!)
            };

            // Bağlantı testi — başlangıçta DB erişilebilir mi?
            var ping = await MenuService.GetAllMenuItemsAsync();
            if (!ping.Success)
                throw new Exception($"DB bağlantısı kurulamadı: {ping.Message}");
        }

        public async Task DisposeAsync()
        {
            // Test sırasında açılan siparişleri temizle (odendi yap)
            foreach (var orderId in _testOrderIds)
            {
                try
                {
                    await Client.Db
                        .Table<OrderModel>()
                        .Where(o => o.Id == orderId)
                        .Set(o => o.Status, "odendi")
                        .Set(o => o.Total, 0.0)
                        .Update();

                    // Bu siparişe ait kalemleri sil
                    var items = await Client.Db
                        .Table<OrderItemModel>()
                        .Where(i => i.OrderId == orderId)
                        .Get();
                    foreach (var item in items.Models)
                        await Client.Db.Table<OrderItemModel>().Where(i => i.Id == item.Id).Delete();
                }
                catch { /* cleanup hatası testi başarısız saymamalı */ }
            }

            // Test sırasında eklenen menü ürünlerini pasif yap
            foreach (var menuItemId in _testMenuItemIds)
            {
                try
                {
                    await Client.Db
                        .Table<MenuItemModel>()
                        .Where(m => m.Id == menuItemId)
                        .Set(m => m.IsActive, false)
                        .Update();
                }
                catch { }
            }
        }
    }

    public class TestSettings
    {
        public int GetMenuItemsMs { get; set; }
        public int GetTablesMs { get; set; }
        public int CreateOrderMs { get; set; }
        public int AddOrderItemMs { get; set; }
        public int CloseOrderMs { get; set; }
        public int FullOrderFlowMs { get; set; }
    }

    /// <summary>
    /// Her testte bağımsız temizleme isteyen testler için — fixture paylaşılamayan durumlar.
    /// </summary>
    public class IsolatedDatabaseFixture : DatabaseFixture { }
}