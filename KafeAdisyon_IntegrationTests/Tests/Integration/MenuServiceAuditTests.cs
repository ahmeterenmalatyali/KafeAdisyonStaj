using FluentAssertions;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.IntegrationTests.Infrastructure;
using KafeAdisyon.Models;
using Xunit;

namespace KafeAdisyon.IntegrationTests.Tests.Integration
{
    [Collection("Database")]
    public class MenuServiceAuditTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fx;
        public MenuServiceAuditTests(DatabaseFixture fx) => _fx = fx;

        // FIX: Guid.NewGuid():N[..6] geçersiz → ToString("N")[..6] kullan
        private static string ShortId() => Guid.NewGuid().ToString("N")[..6];

        [Fact(DisplayName = "DB | Menü: Fiyat güncelleme DB'ye doğru yansır")]
        public async Task UpdatePrice_PersistsToDatabase()
        {
            var addResp = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            { Name = $"Test-Fiyat-{ShortId()}", Category = "Test", Price = 25.0 });
            addResp.Success.Should().BeTrue(addResp.Message);
            var item = addResp.Data!;
            _fx.TrackMenuItem(item.Id);

            var updateResp = await _fx.MenuService.UpdateMenuItemAsync(new UpdateMenuItemRequest
            { Id = item.Id, Name = item.Name, Category = item.Category, Price = 30.0, IsActive = true });
            updateResp.Success.Should().BeTrue(updateResp.Message);

            var allItems = await _fx.MenuService.GetAllMenuItemsAsync();
            var updated = allItems.Data!.FirstOrDefault(m => m.Id == item.Id);
            updated.Should().NotBeNull();
            updated!.Price.Should().BeApproximately(30.0, 0.001);
        }

        [Fact(DisplayName = "DB | Menü: Fiyat aynı kalırsa güncelleme yine başarılı döner")]
        public async Task UpdatePrice_SamePrice_StillSucceeds()
        {
            var addResp = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            { Name = $"Test-AyniF-{ShortId()}", Category = "Test", Price = 20.0 });
            addResp.Success.Should().BeTrue();
            var item = addResp.Data!;
            _fx.TrackMenuItem(item.Id);

            var updateResp = await _fx.MenuService.UpdateMenuItemAsync(new UpdateMenuItemRequest
            { Id = item.Id, Name = item.Name, Category = item.Category, Price = 20.0, IsActive = true });

            updateResp.Success.Should().BeTrue("aynı fiyatla güncelleme de başarılı olmalı");
        }

        [Fact(DisplayName = "DB | Menü: Soft delete — is_active false olur, listeden düşer")]
        public async Task SoftDelete_ItemRemovedFromActiveList()
        {
            var addResp = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            { Name = $"Test-Sil-{ShortId()}", Category = "Test", Price = 15.0 });
            addResp.Success.Should().BeTrue();
            var item = addResp.Data!;
            _fx.TrackMenuItem(item.Id);

            var deleteResp = await _fx.MenuService.DeleteMenuItemAsync(item.Id);
            deleteResp.Success.Should().BeTrue(deleteResp.Message);

            var allItems = await _fx.MenuService.GetAllMenuItemsAsync();
            allItems.Data!.Should().NotContain(m => m.Id == item.Id);
        }

        [Fact(DisplayName = "DB | Menü: Soft delete — DB'de kayıt silinmez, is_active=false olur")]
        public async Task SoftDelete_RecordStillExistsInDatabase()
        {
            var addResp = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            { Name = $"Test-SilDB-{ShortId()}", Category = "Test", Price = 18.0 });
            addResp.Success.Should().BeTrue();
            var item = addResp.Data!;
            _fx.TrackMenuItem(item.Id);

            await _fx.MenuService.DeleteMenuItemAsync(item.Id);

            var dbResult = await _fx.Client.Db
                .Table<MenuItemModel>()
                .Select("id,name,is_active")
                .Where(m => m.Id == item.Id)
                .Get();

            dbResult.Models.Should().HaveCount(1, "soft delete fiziksel silme yapmamalı");
            dbResult.Models[0].IsActive.Should().BeFalse("is_active false olmalı");
        }

        [Fact(DisplayName = "DB | Menü: Yeni ürün ekleme — aktif listede görünür")]
        public async Task AddMenuItem_AppearsInActiveList()
        {
            var name = $"Test-Ekle-{ShortId()}";
            var addResp = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            { Name = name, Category = "Yiyecek", Price = 55.0 });
            addResp.Success.Should().BeTrue(addResp.Message);
            var item = addResp.Data!;
            _fx.TrackMenuItem(item.Id);

            item.Id.Should().NotBeNullOrEmpty();
            item.Name.Should().Be(name);
            item.Price.Should().BeApproximately(55.0, 0.001);

            var allItems = await _fx.MenuService.GetAllMenuItemsAsync();
            allItems.Data!.Should().Contain(m => m.Id == item.Id);
        }

        [Fact(DisplayName = "DB | Menü: Double fiyat precision — 5 ile biten fiyatlar doğru saklanır")]
        public async Task AddMenuItem_PriceEndingIn5_StoredCorrectly()
        {
            var addResp = await _fx.MenuService.AddMenuItemAsync(new AddMenuItemRequest
            { Name = $"Test-Price5-{ShortId()}", Category = "Test", Price = 17.5 });
            addResp.Success.Should().BeTrue();
            var item = addResp.Data!;
            _fx.TrackMenuItem(item.Id);

            var allItems = await _fx.MenuService.GetAllMenuItemsAsync();
            var dbItem = allItems.Data!.FirstOrDefault(m => m.Id == item.Id);
            dbItem.Should().NotBeNull();
            dbItem!.Price.Should().BeApproximately(17.5, 0.001);
        }
    }
}