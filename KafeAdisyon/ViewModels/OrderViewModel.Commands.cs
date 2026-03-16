using System.Windows.Input;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Models;

namespace KafeAdisyon.ViewModels;

public partial class OrderViewModel
{
    public ICommand AddItemCommand => new Command<MenuItemModel>(async (item) =>
    {
        if (item == null) return;
        if (CurrentOrder == null)
            await EnsureOrderCreatedAsync(TableId);

        AddItemOptimistic(item);
        _ = SyncItemToDbAsync(item);
        MenuItemAdded?.Invoke(item);
    });

    public event Action<MenuItemModel>? MenuItemAdded;

    public void RaiseMenuItemAdded(MenuItemModel item) => MenuItemAdded?.Invoke(item);
}
