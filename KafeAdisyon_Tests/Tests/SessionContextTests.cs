using FluentAssertions;
using Xunit;

// SessionContext ana projeden kopyalanmış — test projesi bağımsız çalışır
namespace KafeAdisyon.Infrastructure.Services
{
    /// <summary>
    /// SessionContext — login durumu, rol kontrolü ve Clear() davranışları.
    /// </summary>
    public class SessionContext
    {
        public string UserId { get; private set; } = string.Empty;
        public string UserName { get; private set; } = string.Empty;
        public string Role { get; private set; } = string.Empty;
        public string DeviceName { get; private set; } = string.Empty;
        public string JwtToken { get; private set; } = string.Empty;

        public bool IsLoggedIn => !string.IsNullOrEmpty(UserId);
        public bool IsAdmin => Role == "admin";

        public void SetSession(string userId, string userName, string role,
                               string deviceName, string jwtToken)
        {
            UserId = userId;
            UserName = userName;
            Role = role;
            DeviceName = deviceName;
            JwtToken = jwtToken;
        }

        public void Clear()
        {
            UserId = string.Empty;
            UserName = string.Empty;
            Role = string.Empty;
            DeviceName = string.Empty;
            JwtToken = string.Empty;
        }
    }
}

namespace KafeAdisyon.Tests
{
    using KafeAdisyon.Infrastructure.Services;

    public class SessionContextTests
    {
        private static SessionContext BuildLoggedIn(string role = "admin")
        {
            var ctx = new SessionContext();
            ctx.SetSession("uid-1", "Ahmet Yılmaz", role, "Kasa-1", "jwt-token-abc");
            return ctx;
        }

        // ─── SetSession ───────────────────────────────────────────────────────

        [Fact(DisplayName = "SetSession: Tüm alanlar doğru atanır")]
        public void SetSession_AllFieldsAssignedCorrectly()
        {
            var ctx = new SessionContext();

            ctx.SetSession("uid-42", "Mehmet Demir", "garson", "Garson-Tablet", "token-xyz");

            ctx.UserId.Should().Be("uid-42");
            ctx.UserName.Should().Be("Mehmet Demir");
            ctx.Role.Should().Be("garson");
            ctx.DeviceName.Should().Be("Garson-Tablet");
            ctx.JwtToken.Should().Be("token-xyz");
        }

        [Fact(DisplayName = "SetSession: IsLoggedIn true olur")]
        public void SetSession_IsLoggedIn_BecomesTrue()
        {
            var ctx = new SessionContext();
            ctx.IsLoggedIn.Should().BeFalse("başlangıçta login değil");

            ctx.SetSession("uid-1", "Ad", "admin", "PC", "tok");

            ctx.IsLoggedIn.Should().BeTrue();
        }

        // ─── IsAdmin ──────────────────────────────────────────────────────────

        [Fact(DisplayName = "IsAdmin: admin rolü için true döner")]
        public void IsAdmin_AdminRole_ReturnsTrue()
        {
            var ctx = BuildLoggedIn("admin");
            ctx.IsAdmin.Should().BeTrue();
        }

        [Fact(DisplayName = "IsAdmin: garson rolü için false döner")]
        public void IsAdmin_GarsonRole_ReturnsFalse()
        {
            var ctx = BuildLoggedIn("garson");
            ctx.IsAdmin.Should().BeFalse();
        }

        [Fact(DisplayName = "IsAdmin: boş rol için false döner")]
        public void IsAdmin_EmptyRole_ReturnsFalse()
        {
            var ctx = new SessionContext();
            ctx.IsAdmin.Should().BeFalse();
        }

        // ─── Clear ────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Clear: Tüm alanlar sıfırlanır")]
        public void Clear_ResetsAllFields()
        {
            var ctx = BuildLoggedIn();

            ctx.Clear();

            ctx.UserId.Should().BeEmpty();
            ctx.UserName.Should().BeEmpty();
            ctx.Role.Should().BeEmpty();
            ctx.DeviceName.Should().BeEmpty();
            ctx.JwtToken.Should().BeEmpty();
        }

        [Fact(DisplayName = "Clear: IsLoggedIn false olur")]
        public void Clear_IsLoggedIn_BecomesFalse()
        {
            var ctx = BuildLoggedIn();
            ctx.IsLoggedIn.Should().BeTrue();

            ctx.Clear();

            ctx.IsLoggedIn.Should().BeFalse();
        }

        [Fact(DisplayName = "Clear: IsAdmin false olur")]
        public void Clear_IsAdmin_BecomesFalse()
        {
            var ctx = BuildLoggedIn("admin");
            ctx.IsAdmin.Should().BeTrue();

            ctx.Clear();

            ctx.IsAdmin.Should().BeFalse();
        }

        [Fact(DisplayName = "Clear: Boş context'te çağrılınca hata fırlatmaz")]
        public void Clear_OnEmptyContext_DoesNotThrow()
        {
            var ctx = new SessionContext();
            var act = () => ctx.Clear();
            act.Should().NotThrow();
        }

        // ─── SetSession ardından SetSession ───────────────────────────────────

        [Fact(DisplayName = "SetSession: İkinci çağrı tüm alanları günceller")]
        public void SetSession_CalledTwice_OverwritesPreviousValues()
        {
            var ctx = BuildLoggedIn("admin");

            ctx.SetSession("uid-2", "Fatma Kaya", "garson", "Garson-2", "new-token");

            ctx.UserId.Should().Be("uid-2");
            ctx.UserName.Should().Be("Fatma Kaya");
            ctx.Role.Should().Be("garson");
            ctx.IsAdmin.Should().BeFalse();
        }
    }
}