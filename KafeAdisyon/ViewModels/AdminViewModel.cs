using CommunityToolkit.Mvvm.ComponentModel;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Models;
using System.Collections.ObjectModel;

namespace KafeAdisyon.ViewModels;

/// <summary>
/// Admin ekranı ViewModel'i.
/// DatabaseService bağımlılığı kaldırıldı — IMenuService + ITableService üzerinden çalışır.
/// </summary>
public partial class AdminViewModel : BaseMenuViewModel
{
    private readonly ITableService _tableService;
    private bool _menuLoaded = false;

    [ObservableProperty] private ObservableCollection<TableModel> tables = new();
    [ObservableProperty] private string newItemName = string.Empty;
    [ObservableProperty] private string newItemCategory = string.Empty;
    [ObservableProperty] private string newItemPrice = string.Empty;

    public AdminViewModel(IMenuService menuService, ITableService tableService)
        : base(menuService)
    {
        _tableService = tableService;
    }

    public async Task RefreshTablesAsync()
    {
        var response = await _tableService.GetAllTablesAsync();
        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }
        Tables = new ObservableCollection<TableModel>(response.Data!);
    }

    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            var tablesTask = _tableService.GetAllTablesAsync();
            var menuTask = _menuLoaded
                ? Task.FromResult(KafeAdisyon.Common.BaseResponse<List<MenuItemModel>>
                    .SuccessResult(MenuItems.ToList()))
                : _menuService.GetAllMenuItemsAsync();

            await Task.WhenAll(tablesTask, menuTask);

            var tablesResponse = tablesTask.Result;
            var menuResponse = menuTask.Result;

            if (!tablesResponse.Success)
            {
                StatusMessage = tablesResponse.Message;
                return;
            }
            Tables = new ObservableCollection<TableModel>(tablesResponse.Data!);

            if (!_menuLoaded)
            {
                if (!menuResponse.Success)
                {
                    StatusMessage = menuResponse.Message;
                    return;
                }
                MenuItems = new ObservableCollection<MenuItemModel>(menuResponse.Data!);
                InitializeCategories(menuResponse.Data!);
                _menuLoaded = true;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadMenuAsync()
    {
        var response = await _menuService.GetAllMenuItemsAsync();
        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }

        var menuList = response.Data!;
        MenuItems = new ObservableCollection<MenuItemModel>(menuList);
        Categories = new List<string> { MenuConstants.AllCategories };
        Categories.AddRange(menuList.Select(m => m.Category).Distinct().OrderBy(c => c));
        FilterByCategory(SelectedCategory);
        _menuLoaded = true;
    }

    public async Task<bool> AddMenuItemAsync()
    {
        if (string.IsNullOrWhiteSpace(NewItemName) ||
            string.IsNullOrWhiteSpace(NewItemCategory) ||
            !double.TryParse(NewItemPrice.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double price))
            return false;

        var response = await _menuService.AddMenuItemAsync(new AddMenuItemRequest
        {
            Name = NewItemName.Trim(),
            Category = NewItemCategory.Trim(),
            Price = price
        });

        if (!response.Success)
        {
            StatusMessage = response.Message;
            return false;
        }

        MenuItems.Add(response.Data!);

        var newCat = NewItemCategory.Trim();
        if (!Categories.Contains(newCat))
        {
            int insertIdx = 1;
            while (insertIdx < Categories.Count &&
                   string.Compare(Categories[insertIdx], newCat, StringComparison.CurrentCulture) < 0)
                insertIdx++;
            Categories.Insert(insertIdx, newCat);
        }

        FilterByCategory(SelectedCategory);

        NewItemName = string.Empty;
        NewItemCategory = string.Empty;
        NewItemPrice = string.Empty;
        return true;
    }

    public async Task DeleteMenuItemAsync(string id)
    {
        var response = await _menuService.DeleteMenuItemAsync(id);
        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }

        var item = MenuItems.FirstOrDefault(m => m.Id == id);
        if (item != null) MenuItems.Remove(item);
        FilterByCategory(SelectedCategory);
    }

    public async Task UpdateMenuItemFullAsync(MenuItemModel item)
    {
        var response = await _menuService.UpdateMenuItemAsync(new UpdateMenuItemRequest
        {
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            Price = item.Price,
            IsActive = item.IsActive
        });

        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }

        Categories = new List<string> { MenuConstants.AllCategories };
        Categories.AddRange(MenuItems.Select(m => m.Category).Distinct().OrderBy(c => c));
        FilterByCategory(SelectedCategory);
    }

    public async Task UpdateMenuItemPriceAsync(MenuItemModel item, double newPrice)
    {
        item.Price = newPrice;
        var response = await _menuService.UpdateMenuItemAsync(new UpdateMenuItemRequest
        {
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            Price = newPrice,
            IsActive = item.IsActive
        });

        if (!response.Success)
            StatusMessage = response.Message;
    }

    public TableModel? GetTableByName(string name) =>
        Tables.FirstOrDefault(t => t.Name == name);
}