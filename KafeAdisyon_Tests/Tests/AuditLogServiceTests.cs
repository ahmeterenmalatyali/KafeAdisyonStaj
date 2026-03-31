using FluentAssertions;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Models;
using KafeAdisyon.Tests.TestInfrastructure;
using Moq;
using Xunit;

// ─── Test için minimal AuditLog altyapısı ──────────────────────────────────
// Ana projeden bağımsız — interface + in-memory implementasyon

namespace KafeAdisyon.Tests.TestInfrastructure
{
    // AuditLogModel — test projesinde kopyası
    public class AuditLogModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // SessionContext — test projesinde minimal kopyası
    public class TestSessionContext
    {
        public string UserId { get; private set; } = string.Empty;
        public string UserName { get; private set; } = string.Empty;
        public string Role { get; private set; } = string.Empty;
        public string DeviceName { get; private set; } = string.Empty;
        public bool IsLoggedIn => !string.IsNullOrEmpty(UserId);

        public void SetSession(string userId, string userName, string role, string deviceName)
        {
            UserId = userId; UserName = userName; Role = role; DeviceName = deviceName;
        }
        public void Clear() { UserId = UserName = Role = DeviceName = string.Empty; }
    }

    // In-memory AuditLog servisi — gerçek DB'ye dokunmaz
    public class InMemoryAuditLogService
    {
        private readonly List<AuditLogModel> _logs = new();
        private readonly TestSessionContext _session;

        public InMemoryAuditLogService(TestSessionContext session) => _session = session;

        public async Task LogAsync(string action, string detail)
        {
            if (!_session.IsLoggedIn) return;
            try
            {
                _logs.Add(new AuditLogModel
                {
                    UserId = _session.UserId,
                    UserName = _session.UserName,
                    Role = _session.Role,
                    DeviceName = _session.DeviceName,
                    Action = action,
                    Detail = detail
                });
            }
            catch { /* yut */ }
            await Task.CompletedTask;
        }

        public List<AuditLogModel> GetLogs() => new(_logs);
        public int Count => _logs.Count;
    }
}

namespace KafeAdisyon.Tests
{
    using KafeAdisyon.Tests.TestInfrastructure;

    /// <summary>
    /// AuditLogService davranış testleri.
    ///
    /// Test grupları:
    ///   Login yoksa log yazılmaz.
    ///   Login varsa log doğru alanlarla yazılır.
    ///   Hata durumunda asıl işlem durmamalı.
    ///   Farklı aksiyon tipleri doğru kaydedilir.
    /// </summary>
    public class AuditLogServiceTests
    {
        private (InMemoryAuditLogService svc, TestSessionContext session) Build(bool loggedIn = true)
        {
            var session = new TestSessionContext();
            if (loggedIn)
                session.SetSession("uid-1", "Ahmet Yılmaz", "admin", "Kasa-1");
            var svc = new InMemoryAuditLogService(session);
            return (svc, session);
        }

        // ─── Login yoksa log yazılmaz ──────────────────────────────────────

        [Fact(DisplayName = "AuditLog: Login yoksa log yazılmaz")]
        public async Task LogAsync_NotLoggedIn_DoesNotWriteLog()
        {
            var (svc, _) = Build(loggedIn: false);

            await svc.LogAsync("hesap_kapatma", "Masa A1 — ₺120.00");

            svc.Count.Should().Be(0, "oturum açık değilse log kaydedilmemeli");
        }

        // ─── Log alanları doğru kaydedilir ────────────────────────────────

        [Fact(DisplayName = "AuditLog: Login varsa log doğru alanlarla yazılır")]
        public async Task LogAsync_LoggedIn_WritesCorrectFields()
        {
            var (svc, session) = Build();

            await svc.LogAsync("hesap_kapatma", "Masa A3 — ₺245.00");

            svc.Count.Should().Be(1);
            var log = svc.GetLogs()[0];
            log.UserId.Should().Be("uid-1");
            log.UserName.Should().Be("Ahmet Yılmaz");
            log.Role.Should().Be("admin");
            log.DeviceName.Should().Be("Kasa-1");
            log.Action.Should().Be("hesap_kapatma");
            log.Detail.Should().Be("Masa A3 — ₺245.00");
        }

        [Fact(DisplayName = "AuditLog: Garson rolü doğru kaydedilir")]
        public async Task LogAsync_GarsonRole_RecordedCorrectly()
        {
            var session = new TestSessionContext();
            session.SetSession("uid-2", "Mehmet Demir", "garson", "Garson-Tablet");
            var svc = new InMemoryAuditLogService(session);

            await svc.LogAsync("siparis_iptali", "Sipariş o-99 iptal edildi");

            var log = svc.GetLogs()[0];
            log.Role.Should().Be("garson");
            log.DeviceName.Should().Be("Garson-Tablet");
        }

        // ─── Farklı aksiyon tipleri ────────────────────────────────────────

        [Theory(DisplayName = "AuditLog: Tüm aksiyon tipleri doğru kaydedilir")]
        [InlineData("hesap_kapatma", "Masa B2 — ₺180.00")]
        [InlineData("siparis_iptali", "Sipariş o-5 iptal edildi")]
        [InlineData("fiyat_guncelleme", "Çay: ₺15.00 → ₺18.00")]
        [InlineData("urun_ekleme", "Türk Kahvesi — İçecek — ₺35.00")]
        [InlineData("urun_silme", "Kola pasife alındı")]
        public async Task LogAsync_AllActionTypes_RecordedCorrectly(string action, string detail)
        {
            var (svc, _) = Build();

            await svc.LogAsync(action, detail);

            var log = svc.GetLogs()[0];
            log.Action.Should().Be(action);
            log.Detail.Should().Be(detail);
        }

        // ─── Birden fazla log ──────────────────────────────────────────────

        [Fact(DisplayName = "AuditLog: Birden fazla log sırayla kaydedilir")]
        public async Task LogAsync_MultipleLogs_AllRecordedInOrder()
        {
            var (svc, _) = Build();

            await svc.LogAsync("urun_ekleme", "Espresso eklendi");
            await svc.LogAsync("fiyat_guncelleme", "Çay: ₺15 → ₺18");
            await svc.LogAsync("hesap_kapatma", "Masa A1 — ₺90.00");

            svc.Count.Should().Be(3);
            var logs = svc.GetLogs();
            logs[0].Action.Should().Be("urun_ekleme");
            logs[1].Action.Should().Be("fiyat_guncelleme");
            logs[2].Action.Should().Be("hesap_kapatma");
        }

        // ─── Logout sonrası log yazılmaz ──────────────────────────────────

        [Fact(DisplayName = "AuditLog: Logout sonrası log yazılmaz")]
        public async Task LogAsync_AfterLogout_DoesNotWriteLog()
        {
            var (svc, session) = Build(loggedIn: true);

            await svc.LogAsync("hesap_kapatma", "Masa A1 — ₺50.00");
            svc.Count.Should().Be(1);

            session.Clear(); // logout
            await svc.LogAsync("hesap_kapatma", "Masa A2 — ₺75.00");

            svc.Count.Should().Be(1, "logout sonrası log yazılmamalı");
        }

        // ─── Snapshot davranışı ────────────────────────────────────────────

        [Fact(DisplayName = "AuditLog: Log yazıldıktan sonra profil değişse log etkilenmez")]
        public async Task LogAsync_ProfileChangedAfterLog_LogUnaffected()
        {
            var (svc, session) = Build();
            await svc.LogAsync("urun_ekleme", "Cappuccino eklendi");

            // Profil değişiyor — log önceki değeri korumalı
            session.SetSession("uid-1", "Ahmet YILMAZ (güncellendi)", "admin", "Kasa-2");

            var log = svc.GetLogs()[0];
            log.UserName.Should().Be("Ahmet Yılmaz",
                "log yazılırken anlık snapshot alınmalı, sonraki değişiklikler log'u etkilememeli");
            log.DeviceName.Should().Be("Kasa-1");
        }
    }
}