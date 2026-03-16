using CommunityToolkit.Mvvm.ComponentModel;
using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Models;
using System.Collections.ObjectModel;

namespace KafeAdisyon.ViewModels;

/// <summary>
/// Sipariş ekranı ViewModel'i.
/// DatabaseService bağımlılığı kaldırıldı — IOrderService + IMenuService üzerinden çalışır.
/// </summary>
public partial class OrderViewModel : BaseMenuViewModel
{
    private readonly IOrderService _orderService;
    private readonly ITableService _tableService;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    [ObservableProperty] private string tableId = string.Empty;
    [ObservableProperty] private string tableName = string.Empty;
    [ObservableProperty] private ObservableCollection<OrderItemModel> orderItems = new();
    [ObservableProperty] private double total;
    [ObservableProperty] private OrderModel? currentOrder;

    public Dictionary<string, MenuItemModel> MenuItemLookup { get; private set; } = new();

    public OrderViewModel(IMenuService menuService, IOrderService orderService, ITableService tableService)
        : base(menuService)
    {
        _orderService = orderService;
        _tableService = tableService;
    }

    public async Task LoadAsync(string tableId, string tableName)
    {
        TableId = tableId;
        TableName = tableName;
        IsLoading = true;
        StatusMessage = "Yükleniyor...";

        try
        {
            var menuResponse = await _menuService.GetAllMenuItemsAsync();
            if (!menuResponse.Success)
            {
                StatusMessage = menuResponse.Message;
                return;
            }

            var items = menuResponse.Data!;
            MenuItems = new ObservableCollection<MenuItemModel>(items);
            MenuItemLookup = items.ToDictionary(i => i.Id);
            InitializeCategories(items);

            var orderResponse = await _orderService.GetActiveOrderByTableAsync(tableId);
            if (!orderResponse.Success)
            {
                StatusMessage = orderResponse.Message;
                return;
            }

            CurrentOrder = orderResponse.Data;

            if (CurrentOrder != null)
            {
                var itemsResponse = await _orderService.GetOrderItemsAsync(CurrentOrder.Id);
                if (!itemsResponse.Success)
                {
                    StatusMessage = itemsResponse.Message;
                    return;
                }

                var orderItemList = itemsResponse.Data!;
                if (orderItemList.Count == 0)
                {
                    await _orderService.CloseOrderAsync(new CloseOrderRequest
                    {
                        OrderId = CurrentOrder.Id,
                        TableId = TableId,
                        FinalTotal = 0
                    });
                    CurrentOrder = null;
                    OrderItems = new ObservableCollection<OrderItemModel>();
                    Total = 0;
                }
                else
                {
                    OrderItems = new ObservableCollection<OrderItemModel>(orderItemList);
                    Total = OrderItems.Sum(i => i.Price * i.Quantity);
                }
            }

            StatusMessage = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadOrderItemsAsync()
    {
        if (CurrentOrder == null)
        {
            OrderItems = new ObservableCollection<OrderItemModel>();
            Total = 0;
            return;
        }

        var response = await _orderService.GetOrderItemsAsync(CurrentOrder.Id);
        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }

        var orderItemList = response.Data!;
        if (orderItemList.Count == 0)
        {
            await _orderService.CloseOrderAsync(new CloseOrderRequest
            {
                OrderId = CurrentOrder.Id,
                TableId = TableId,
                FinalTotal = 0
            });
            CurrentOrder = null;
            OrderItems = new ObservableCollection<OrderItemModel>();
            Total = 0;
        }
        else
        {
            OrderItems = new ObservableCollection<OrderItemModel>(orderItemList);
            Total = OrderItems.Sum(i => i.Price * i.Quantity);
        }
    }

    public async Task EnsureOrderCreatedAsync(string tableId)
    {
        if (CurrentOrder != null) return;
        await _syncLock.WaitAsync();
        try
        {
            if (CurrentOrder != null) return;

            var orderResponse = await _orderService.CreateOrderAsync(tableId);
            if (!orderResponse.Success)
            {
                StatusMessage = orderResponse.Message;
                return;
            }
            CurrentOrder = orderResponse.Data;

            await _tableService.UpdateTableStatusAsync(new UpdateTableStatusRequest
            {
                TableId = tableId,
                Status = "dolu"
            });
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public void AddItemOptimistic(MenuItemModel menuItem)
    {
        if (CurrentOrder == null) return;

        var existing = OrderItems.FirstOrDefault(i => i.MenuItemId == menuItem.Id);
        if (existing != null)
        {
            existing.Quantity += 1;
        }
        else
        {
            OrderItems.Add(new OrderItemModel
            {
                Id = "_temp_" + menuItem.Id,
                OrderId = CurrentOrder.Id,
                MenuItemId = menuItem.Id,
                Quantity = 1,
                Price = menuItem.Price
            });
        }
        Total = OrderItems.Sum(i => i.Price * i.Quantity);
    }

    public async Task SyncItemToDbAsync(MenuItemModel menuItem)
    {
        await _syncLock.WaitAsync();
        try
        {
            if (CurrentOrder == null) return;

            var existing = OrderItems.FirstOrDefault(i =>
                i.MenuItemId == menuItem.Id && !i.Id.StartsWith("_temp_"));

            if (existing != null)
            {
                var updateResponse = await _orderService.UpdateOrderItemQuantityAsync(
                    new UpdateOrderItemQuantityRequest
                    {
                        ItemId = existing.Id,
                        Quantity = existing.Quantity
                    });

                if (!updateResponse.Success)
                    StatusMessage = updateResponse.Message;
            }
            else
            {
                var tempItem = OrderItems.FirstOrDefault(i => i.Id == "_temp_" + menuItem.Id);
                int qty = tempItem?.Quantity ?? 1;

                var addResponse = await _orderService.AddOrderItemAsync(new AddOrderItemRequest
                {
                    OrderId = CurrentOrder.Id,
                    MenuItemId = menuItem.Id,
                    Quantity = qty,
                    Price = menuItem.Price
                });

                if (!addResponse.Success)
                {
                    StatusMessage = addResponse.Message;
                    return;
                }

                if (tempItem != null)
                {
                    var inserted = addResponse.Data!;
                    inserted.Quantity = qty;
                    var idx = OrderItems.IndexOf(tempItem);
                    if (idx >= 0)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (idx < OrderItems.Count)
                                OrderItems[idx] = inserted;
                        });
                    }
                }
            }
            Total = OrderItems.Sum(i => i.Price * i.Quantity);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task RemoveItemAsync(OrderItemModel item)
    {
        if (item.Quantity > 1)
        {
            var updateResponse = await _orderService.UpdateOrderItemQuantityAsync(
                new UpdateOrderItemQuantityRequest
                {
                    ItemId = item.Id,
                    Quantity = item.Quantity - 1
                });

            if (!updateResponse.Success)
            {
                StatusMessage = updateResponse.Message;
                return;
            }
            item.Quantity -= 1;
        }
        else
        {
            var removeResponse = await _orderService.RemoveOrderItemAsync(item.Id);
            if (!removeResponse.Success)
            {
                StatusMessage = removeResponse.Message;
                return;
            }
            OrderItems.Remove(item);
        }

        Total = OrderItems.Sum(i => i.Price * i.Quantity);

        if (OrderItems.Count == 0 && CurrentOrder != null)
        {
            await CloseOrderAsync();
        }
    }

    public async Task RemoveItemDbOnlyAsync(OrderItemModel item)
    {
        var response = await _orderService.RemoveOrderItemAsync(item.Id);
        if (!response.Success)
            StatusMessage = response.Message;
    }

    public async Task RemoveItemFullAsync(OrderItemModel item, bool skipAutoClose = false)
    {
        var response = await _orderService.RemoveOrderItemAsync(item.Id);
        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }

        OrderItems.Remove(item);
        Total = OrderItems.Sum(i => i.Price * i.Quantity);

        if (!skipAutoClose && OrderItems.Count == 0 && CurrentOrder != null)
        {
            await CloseOrderAsync();
        }
    }

    public async Task SetItemQuantityAsync(OrderItemModel item, int newQuantity)
    {
        var response = await _orderService.UpdateOrderItemQuantityAsync(
            new UpdateOrderItemQuantityRequest
            {
                ItemId = item.Id,
                Quantity = newQuantity
            });

        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }
        item.Quantity = newQuantity;
    }

    public void RecalcTotal()
    {
        Total = OrderItems.Sum(i => i.Price * i.Quantity);
    }

    public async Task CloseOrderAsync()
    {
        if (CurrentOrder == null) return;

        var response = await _orderService.CloseOrderAsync(new CloseOrderRequest
        {
            OrderId = CurrentOrder.Id,
            TableId = TableId,
            FinalTotal = Total
        });

        if (!response.Success)
        {
            StatusMessage = response.Message;
            return;
        }

        CurrentOrder = null;
        OrderItems.Clear();
        Total = 0;
    }
}
