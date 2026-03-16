using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Models;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Menü ürünü CRUD işlemlerinin implementasyonu.
/// AdministrativeAffairsService'deki PosService yapısıyla aynı pattern.
/// </summary>
public class MenuService : IMenuService
{
    private readonly DatabaseClient _client;

    public MenuService(DatabaseClient client)
    {
        _client = client;
    }

    public async Task<BaseResponse<List<MenuItemModel>>> GetAllMenuItemsAsync()
    {
        try
        {
            var result = await _client.Db
                .Table<MenuItemModel>()
                .Select(DatabaseClient.MenuItemColumns)
                .Where(m => m.IsActive == true)
                .Get();

            return BaseResponse<List<MenuItemModel>>.SuccessResult(
                result.Models, "Menü ürünleri başarıyla getirildi");
        }
        catch (Exception ex)
        {
            return BaseResponse<List<MenuItemModel>>.ErrorResult(
                $"Menü ürünleri getirilemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<MenuItemModel>> AddMenuItemAsync(AddMenuItemRequest request)
    {
        try
        {
            var item = new MenuItemModel
            {
                Name = request.Name,
                Category = request.Category,
                Price = request.Price
            };

            var result = await _client.Db.Table<MenuItemModel>().Insert(item);
            var inserted = result.Models.First();

            return BaseResponse<MenuItemModel>.SuccessResult(inserted, "Ürün başarıyla eklendi");
        }
        catch (Exception ex)
        {
            return BaseResponse<MenuItemModel>.ErrorResult($"Ürün eklenemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<object>> UpdateMenuItemAsync(UpdateMenuItemRequest request)
    {
        try
        {
            var item = new MenuItemModel
            {
                Id = request.Id,
                Name = request.Name,
                Category = request.Category,
                Price = request.Price,
                IsActive = request.IsActive
            };

            await _client.Db.Table<MenuItemModel>().Update(item);

            return BaseResponse<object>.SuccessResult(null, "Ürün başarıyla güncellendi");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Ürün güncellenemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<object>> DeleteMenuItemAsync(string id)
    {
        try
        {
            await _client.Db
                .Table<MenuItemModel>()
                .Where(m => m.Id == id)
                .Set(m => m.IsActive, false)
                .Update();

            return BaseResponse<object>.SuccessResult(null, "Ürün başarıyla silindi");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Ürün silinemedi: {ex.Message}");
        }
    }
}
