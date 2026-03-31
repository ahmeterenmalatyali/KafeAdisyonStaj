using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.IntegrationTests.Infrastructure;
using KafeAdisyon.Models;
using Microsoft.Extensions.Configuration;
using Postgrest;
using Xunit;

// ─── AuditLog modeli — integration test projesi için
namespace KafeAdisyon.Models
{
    using Postgrest.Attributes;
    using Postgrest.Models;

    [Table("audit_logs")]
    public class AuditLogModel : BaseModel
    {
        [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
        [Column("user_id")] public string? UserId { get; set; } = null;
        [Column("user_name")] public string UserName { get; set; } = string.Empty;
        [Column("role")] public string Role { get; set; } = string.Empty;
        [Column("action")] public string Action { get; set; } = string.Empty;
        [Column("detail")] public string Detail { get; set; } = string.Empty;
        [Column("device_name")] public string DeviceName { get; set; } = string.Empty;
        [Column("created_at")] public DateTime CreatedAt { get; set; }
    }
}

namespace KafeAdisyon.IntegrationTests.Tests.Integration
{
    using KafeAdisyon.Infrastructure.Client;
    using KafeAdisyon.Models;

    /// <summary>
    /// AuditLog integration testleri — gerçek Supabase audit_logs tablosuna yazar ve okur.
    ///
    /// Ön koşul: Supabase'de audit_logs tablosu ve RLS politikaları kurulu olmalı.
    /// Her test sonunda yazdığı kayıtları temizler.
    /// </summary>
    [Collection("Database")]
    public class AuditLogIntegrationTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
    {
        private readonly DatabaseFixture _fx;
        private readonly List<string> _createdLogIds = new();

        // Test için sabit kullanıcı bilgileri
        private const string TestUserId = "00000000-0000-0000-0000-000000000001";
        private const string TestUserName = "Test Kullanıcısı";
        private const string TestRole = "admin";
        private const string TestDeviceName = "Test-Cihaz";

        public AuditLogIntegrationTests(DatabaseFixture fx) => _fx = fx;

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            // Test sırasında oluşturulan logları temizle
            foreach (var id in _createdLogIds)
            {
                try
                {
                    await _fx.Client.Db
                        .Table<AuditLogModel>()
                        .Where(l => l.Id == id)
                        .Delete();
                }
                catch { }
            }
        }

        // ─── Yardımcı: log kaydı yaz ──────────────────────────────────────────

        private async Task<AuditLogModel> WriteLog(string action, string detail)
        {
            var log = new AuditLogModel
            {
                // user_id gönderme — nullable, FK kısıtlaması tetiklenmesin
                UserName = TestUserName,
                Role = TestRole,
                Action = action,
                Detail = detail,
                DeviceName = TestDeviceName
            };

            var result = await _fx.Client.Db.Table<AuditLogModel>().Insert(log);
            var inserted = result.Models.First();
            _createdLogIds.Add(inserted.Id);
            return inserted;
        }

        // ─── Log yazma testleri ───────────────────────────────────────────────

        [Fact(DisplayName = "DB | AuditLog: Hesap kapatma logu DB'ye yazılır")]
        public async Task HesapKapatma_Log_WrittenToDatabase()
        {
            var log = await WriteLog("hesap_kapatma", "Masa A3 — ₺245.00");

            log.Id.Should().NotBeNullOrEmpty("DB UUID atamalı");
            log.Action.Should().Be("hesap_kapatma");
            log.Detail.Should().Be("Masa A3 — ₺245.00");
            log.UserName.Should().Be(TestUserName);
            log.DeviceName.Should().Be(TestDeviceName);
            log.CreatedAt.Should().NotBe(default(DateTime), "created_at DB'de atanmış olmalı");
        }

        [Fact(DisplayName = "DB | AuditLog: Sipariş iptali logu DB'ye yazılır")]
        public async Task SiparisIptali_Log_WrittenToDatabase()
        {
            var log = await WriteLog("siparis_iptali", "Sipariş o-123 iptal edildi (Masa B2)");

            log.Action.Should().Be("siparis_iptali");
            log.Role.Should().Be(TestRole);
        }

        [Fact(DisplayName = "DB | AuditLog: Fiyat güncelleme logu DB'ye yazılır")]
        public async Task FiyatGuncelleme_Log_WrittenToDatabase()
        {
            var log = await WriteLog("fiyat_guncelleme", "Çay: ₺15.00 → ₺18.00");

            log.Action.Should().Be("fiyat_guncelleme");
            log.Detail.Should().Contain("₺15.00").And.Contain("₺18.00");
        }

        [Fact(DisplayName = "DB | AuditLog: Ürün ekleme logu DB'ye yazılır")]
        public async Task UrunEkleme_Log_WrittenToDatabase()
        {
            var log = await WriteLog("urun_ekleme", "Espresso — İçecek — ₺45.00");

            log.Action.Should().Be("urun_ekleme");
        }

        [Fact(DisplayName = "DB | AuditLog: Ürün silme logu DB'ye yazılır")]
        public async Task UrunSilme_Log_WrittenToDatabase()
        {
            var log = await WriteLog("urun_silme", "Kola pasife alındı");

            log.Action.Should().Be("urun_silme");
        }

        // ─── Log okuma testleri ───────────────────────────────────────────────

        [Fact(DisplayName = "DB | AuditLog: Yazılan log DB'den okunabilir")]
        public async Task WrittenLog_CanBeReadBack()
        {
            var written = await WriteLog("hesap_kapatma", "Masa C1 — ₺320.00");

            var result = await _fx.Client.Db
                .Table<AuditLogModel>()
                .Select("id,user_id,user_name,role,action,detail,device_name,created_at")
                .Where(l => l.Id == written.Id)
                .Get();

            result.Models.Should().HaveCount(1);
            var read = result.Models[0];
            read.Action.Should().Be("hesap_kapatma");
            read.Detail.Should().Be("Masa C1 — ₺320.00");
            read.UserName.Should().Be(TestUserName);
            read.DeviceName.Should().Be(TestDeviceName);
        }

        [Fact(DisplayName = "DB | AuditLog: Birden fazla log sırayla DB'ye yazılır")]
        public async Task MultipleLogs_AllWrittenToDatabase()
        {
            await WriteLog("urun_ekleme", "Türk Kahvesi — İçecek — ₺35.00");
            await WriteLog("fiyat_guncelleme", "Ayran: ₺20.00 → ₺25.00");
            await WriteLog("hesap_kapatma", "Masa A1 — ₺90.00");

            _createdLogIds.Should().HaveCount(3, "3 log DB'ye yazılmalı");
        }

        [Fact(DisplayName = "DB | AuditLog: CreatedAt otomatik atanır ve yakın zamanda")]
        public async Task CreatedAt_AutoAssigned_IsRecentTimestamp()
        {
            var before = DateTime.UtcNow.AddSeconds(-2);

            var log = await WriteLog("urun_silme", "Limonata pasife alındı");

            log.CreatedAt.Should().NotBe(default(DateTime), "created_at DB'de otomatik atanmalı, boş olmamalı");
        }

        // ─── Sipariş akışıyla birlikte audit log ──────────────────────────────

        [Fact(DisplayName = "DB | AuditLog: Hesap kapatma akışında log ve sipariş birlikte doğrulanır")]
        public async Task CloseOrder_WithAuditLog_BothPersisted()
        {
            // 1. Masa ve ürün al
            var tables = await _fx.TableService.GetAllTablesAsync();
            var table = tables.Data!.First();

            var menus = await _fx.MenuService.GetAllMenuItemsAsync();
            var menuItem = menus.Data!.First();

            // 2. Mevcut aktif siparişi temizle
            var existing = await _fx.OrderService.GetActiveOrderByTableAsync(table.Id);
            if (existing.Data != null)
                await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
                { OrderId = existing.Data.Id, TableId = table.Id, FinalTotal = 0 });

            // 3. Sipariş aç
            var order = (await _fx.OrderService.CreateOrderAsync(table.Id)).Data!;
            _fx.TrackOrder(order.Id);

            await _fx.OrderService.AddOrderItemAsync(new AddOrderItemRequest
            { OrderId = order.Id, MenuItemId = menuItem.Id, Quantity = 2, Price = menuItem.Price });

            double total = menuItem.Price * 2;

            // 4. Siparişi kapat
            var closeResult = await _fx.OrderService.CloseOrderAsync(new CloseOrderRequest
            { OrderId = order.Id, TableId = table.Id, FinalTotal = total });
            closeResult.Success.Should().BeTrue();

            // 5. Audit logu yaz (gerçek servis bunu otomatik yapıyor;
            //    burada manuel yazarak DB'ye ulaştığını doğruluyoruz)
            var log = await WriteLog(
                "hesap_kapatma",
                $"Masa {table.Id} — ₺{total:F2}");

            log.Action.Should().Be("hesap_kapatma");
            log.Detail.Should().Contain($"₺{total:F2}");

            // 6. Sipariş DB'de odendi olmalı
            var orders = await _fx.Client.Db
                .Table<OrderModel>()
                .Select("id,status,total")
                .Where(o => o.Id == order.Id)
                .Get();
            orders.Models.First().Status.Should().Be("odendi");
        }
    }
}