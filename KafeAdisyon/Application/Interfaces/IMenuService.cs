using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Common;
using KafeAdisyon.Models;

namespace KafeAdisyon.Application.Interfaces;

/// <summary>
/// Menü ürün işlemleri için servis sözleşmesi.
/// </summary>
public interface IMenuService
{
    Task<BaseResponse<List<MenuItemModel>>> GetAllMenuItemsAsync();
    Task<BaseResponse<MenuItemModel>> AddMenuItemAsync(AddMenuItemRequest request);
    Task<BaseResponse<object>> UpdateMenuItemAsync(UpdateMenuItemRequest request);
    Task<BaseResponse<object>> DeleteMenuItemAsync(string id);
}
