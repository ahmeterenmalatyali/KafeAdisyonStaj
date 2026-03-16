using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.IntegrationTests.Infrastructure;
using Xunit;

namespace KafeAdisyon.IntegrationTests.Tests.Integration
{
    [Collection("Database")]
    public class MenuServiceIntegrationTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fx;
        public MenuServiceIntegrationTests(DatabaseFixture fx) => _fx = fx;

        // ─── OKUMA ─────────────────────────────────────────────────

        [Fact(DisplayName = "DB | Menü: Aktif ürünler listelenir, boş değil")]
        public async Task GetAllMenuItems_ReturnsActiveItems()
        {
            var response = await _fx.MenuService.GetAllMenuItemsAsync();

            response.Success.Should().BeTrue(response.Message);
            response.Data.Should().NotBeNull();
            response.Data!.Should().NotBeEmpty("veritabanında en az 1 aktif ürün olmalı");
        }

        [Fact(DisplayName = "DB | Menü: Tüm dönen ürünler IsActive=true")]
        public async Task GetAllMenuItems_OnlyActiveItems()
        {
            var response = await _fx.MenuService.GetAllMenuItemsAsync();

            response.Success.Should().BeTrue(response.Message);
            response.Data!.Should().OnlyContain(m => m.IsActive,
                "pasif ürünler filtrelenmiş olmalı");
        }

        [Fact(DisplayName = "DB | Menü: Her ürünün ID, Name ve Price alanları dolu")]
        public async Task GetAllMenuItems_AllFieldsPopulated()
        {
            var response = await _fx.MenuService.GetAllMenuItemsAsync();

            response.Success.Should().BeTrue(response.Message);
            foreach (var item in response.Data!)
            {
                item.Id.Should().NotBeNullOrEmpty($"'{item.Name}' ürününün ID'si boş");
                item.Name.Should().NotBeNullOrEmpty();
                item.Price.Should().BeGreaterThan(0, $"'{item.Name}' fiyatı 0'dan büyük olmalı");
                item.Category.Should().NotBeNullOrEmpty($"'{item.Name}' kategorisi boş");
            }
        }

        // ─── EKLEME & SİLME ─────────────────────────────────────────

        [Fact(DisplayName = "DB | Menü: Yeni ürün eklenir, ID atanır, geri okunabilir")]
        public async Task AddMenuItem_CreatesAndReturnsWithId()
        {
            var testName = $"TEST_URUN_{Guid.NewGuid():N}".Substring(0, 20);
            var addResponse = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            {
                Name = testName,
                Category = "Test Kategori",
                Price = 99.90
            });

            addResponse.Success.Should().BeTrue(addResponse.Message);
            addResponse.Data.Should().NotBeNull();
            addResponse.Data!.Id.Should().NotBeNullOrEmpty("DB tarafından UUID atanmalı");
            addResponse.Data.Name.Should().Be(testName);
            addResponse.Data.Price.Should().Be(99.90);

            _fx.TrackMenuItem(addResponse.Data.Id);

            // DB'den geri oku — gerçekten var mı?
            var allItems = await _fx.MenuService.GetAllMenuItemsAsync();
            allItems.Data!.Should().Contain(m => m.Id == addResponse.Data.Id,
                "eklenen ürün listede görünmeli");
        }

        [Fact(DisplayName = "DB | Menü: Güncelleme — isim ve fiyat değişir")]
        public async Task UpdateMenuItem_ChangesNameAndPrice()
        {
            var originalName = $"TEST_GNC_{Guid.NewGuid():N}".Substring(0, 20);
            var addResponse = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            {
                Name = originalName, Category = "Test", Price = 50
            });
            addResponse.Success.Should().BeTrue(addResponse.Message);
            _fx.TrackMenuItem(addResponse.Data!.Id);

            var updatedName = $"TEST_GNC_UPDATED_{Guid.NewGuid():N}".Substring(0, 25);
            var updateResponse = await _fx.MenuService.UpdateMenuItemAsync(new UpdateMenuItemRequest
            {
                Id = addResponse.Data.Id,
                Name = updatedName,
                Category = "Test",
                Price = 75,
                IsActive = true
            });

            updateResponse.Success.Should().BeTrue(updateResponse.Message);

            // DB'den geri oku
            var allItems = await _fx.MenuService.GetAllMenuItemsAsync();
            var updated = allItems.Data!.FirstOrDefault(m => m.Id == addResponse.Data.Id);
            updated.Should().NotBeNull("güncellenen ürün hâlâ aktif ve listede olmalı");
            updated!.Name.Should().Be(updatedName);
            updated.Price.Should().Be(75);
        }

        [Fact(DisplayName = "DB | Menü: Silme — ürün pasif olur, listede görünmez")]
        public async Task DeleteMenuItem_SoftDelete_DisappearsFromActiveList()
        {
            var testName = $"TEST_SIL_{Guid.NewGuid():N}".Substring(0, 20);
            var addResponse = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            {
                Name = testName, Category = "Test", Price = 30
            });
            addResponse.Success.Should().BeTrue(addResponse.Message);
            var addedId = addResponse.Data!.Id;
            _fx.TrackMenuItem(addedId); // cleanup zaten pasif yapıyor, sorun yok

            var deleteResponse = await _fx.MenuService.DeleteMenuItemAsync(addedId);
            deleteResponse.Success.Should().BeTrue(deleteResponse.Message);

            // Aktif listede artık görünmemeli
            var allItems = await _fx.MenuService.GetAllMenuItemsAsync();
            allItems.Data!.Should().NotContain(m => m.Id == addedId,
                "soft-delete sonrası ürün aktif listede olmamalı");
        }
    }
}
