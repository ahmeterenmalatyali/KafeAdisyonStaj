using CommunityToolkit.Mvvm.ComponentModel;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Models;
using System.Collections.ObjectModel;

namespace KafeAdisyon.ViewModels;

/// <summary>
/// OrderViewModel ve AdminViewModel'ın ortak menü filtreleme mantığı.
/// DatabaseService bağımlılığı kaldırıldı — IMenuService üzerinden çalışır.
/// </summary>
public abstract partial class BaseMenuViewModel : ObservableObject
{
    protected readonly IMenuService _menuService;

    [ObservableProperty] private ObservableCollection<MenuItemModel> menuItems = new();
    [ObservableProperty] private ObservableCollection<MenuItemModel> filteredMenuItems = new();
    [ObservableProperty] private string selectedCategory = MenuConstants.AllCategories;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = string.Empty;

    public List<string> Categories { get; protected set; } = new();

    protected BaseMenuViewModel(IMenuService menuService)
    {
        _menuService = menuService;
    }

    public virtual void FilterByCategory(string category)
    {
        SelectedCategory = category;
        var source = category == MenuConstants.AllCategories
            ? MenuItems
            : MenuItems.Where(m => m.Category == category);

        FilteredMenuItems = new ObservableCollection<MenuItemModel>(source);
    }

    protected void InitializeCategories(List<MenuItemModel> items)
    {
        Categories = new List<string> { MenuConstants.AllCategories };
        Categories.AddRange(items.Select(i => i.Category).Distinct().OrderBy(c => c));
        FilterByCategory(MenuConstants.AllCategories);
    }
}
