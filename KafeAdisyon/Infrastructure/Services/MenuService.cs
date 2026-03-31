using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Models;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Menü ürünü CRUD işlemlerinin implementasyonu.
/// Fiyat güncelleme, ürün ekleme ve silme IAuditLogService üzerinden loglanır.
/// </summary>
public class MenuService : IMenuService
{
    private readonly DatabaseClient _client;
    private readonly IAuditLogService _audit;

    public MenuService(DatabaseClient client, IAuditLogService audit)
    {
        _client = client;
        _audit = audit;
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

            // ── Audit Log ──────────────────────────────────────────
            await _audit.LogAsync(
                action: "urun_ekleme",
                detail: $"{inserted.Name} — {inserted.Category} — ₺{inserted.Price:F2}");

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
            // Fiyat değişti mi kontrol için eski kaydı çek
            var oldResult = await _client.Db
                .Table<MenuItemModel>()
                .Select(DatabaseClient.MenuItemColumns)
                .Where(m => m.Id == request.Id)
                .Get();
            var old = oldResult.Models.FirstOrDefault();

            var item = new MenuItemModel
            {
                Id = request.Id,
                Name = request.Name,
                Category = request.Category,
                Price = request.Price,
                IsActive = request.IsActive
            };

            await _client.Db.Table<MenuItemModel>().Update(item);

            // ── Audit Log — fiyat değiştiyse özel log ──────────────
            if (old != null && Math.Abs(old.Price - request.Price) > 0.001)
            {
                await _audit.LogAsync(
                    action: "fiyat_guncelleme",
                    detail: $"{request.Name}: ₺{old.Price:F2} → ₺{request.Price:F2}");
            }

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
            // Soft delete — adı log için önce çek
            var oldResult = await _client.Db
                .Table<MenuItemModel>()
                .Select(DatabaseClient.MenuItemColumns)
                .Where(m => m.Id == id)
                .Get();
            var old = oldResult.Models.FirstOrDefault();

            await _client.Db
                .Table<MenuItemModel>()
                .Where(m => m.Id == id)
                .Set(m => m.IsActive, false)
                .Update();

            // ── Audit Log ──────────────────────────────────────────
            var name = old?.Name ?? id;
            await _audit.LogAsync(
                action: "urun_silme",
                detail: $"{name} pasife alındı");

            return BaseResponse<object>.SuccessResult(null, "Ürün başarıyla silindi");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Ürün silinemedi: {ex.Message}");
        }
    }
}