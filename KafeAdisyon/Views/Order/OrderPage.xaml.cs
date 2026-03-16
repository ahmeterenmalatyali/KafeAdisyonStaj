using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Models;
using KafeAdisyon.ViewModels;

namespace KafeAdisyon.Views.Order;

public partial class OrderPage : ContentPage
{
    private OrderViewModel _vm;
    private readonly string _tableId;
    private readonly string _tableName;
    private readonly bool _isReadOnly;

    private bool _layoutConfigured = false;
    private bool _menuLoaded = false;
    private bool _skipNextReload = false; // SplitBill sonrası OnAppearing reload'unu engeller

    // Kategori butonları için cache — F-05 benzeri
    private readonly Dictionary<string, Button> _categoryBtnMap = new();

    // Sipariş satırları için incremental-render cache
    private readonly Dictionary<string, (Grid Row, Label DetailLabel, BoxView Separator)> _orderRowMap = new();

    public OrderPage(string tableId, string tableName, bool isReadOnly = false)
    {
        InitializeComponent();
        _tableId = tableId;
        _tableName = tableName;
        _isReadOnly = isReadOnly;

        var menuService = Handler?.MauiContext?.Services.GetService<IMenuService>()
                        ?? IPlatformApplication.Current!.Services.GetService<IMenuService>()!;
        var orderService = Handler?.MauiContext?.Services.GetService<IOrderService>()
                        ?? IPlatformApplication.Current!.Services.GetService<IOrderService>()!;
        var tableService = Handler?.MauiContext?.Services.GetService<ITableService>()
                        ?? IPlatformApplication.Current!.Services.GetService<ITableService>()!;
        _vm = new OrderViewModel(menuService, orderService, tableService);

        // D-1: ViewModel event'i dinle — item eklenince sepet panelini güncelle
        _vm.MenuItemAdded += OnMenuItemAdded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_layoutConfigured)
        {
            ConfigureLayout();
            _layoutConfigured = true;
        }

        TableNameLabel.Text = _tableName;

        if (!_menuLoaded)
        {
            await _vm.LoadAsync(_tableId, _tableName);
            BuildCategoryButtons();
            ApplyCollectionViewFilter();
            _menuLoaded = true;
        }
        else if (_skipNextReload)
        {
            // SplitBill DB yazımı bitti, UI zaten güncel — reload atla
            _skipNextReload = false;
        }
        else
        {
            await _vm.ReloadOrderItemsAsync();
        }

        BuildOrderItems();
        UpdateTotal();
        StatusLabel.Text = _vm.StatusMessage == string.Empty ? "Aktif" : _vm.StatusMessage;
    }

    private void ConfigureLayout()
    {
        if (_isReadOnly)
        {
            MenuPanel.IsVisible = false;
            ContentGrid.ColumnDefinitions[0].Width = GridLength.Star;
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
            Grid.SetColumn(OrderPanel, 0);
            Grid.SetColumnSpan(OrderPanel, 2);
            OrderPanel.WidthRequest = -1;
            OrderPanel.HorizontalOptions = LayoutOptions.Fill;

            CancelOrderBtn.IsVisible = false;
            SplitBillBtn.IsVisible = true;
            Grid.SetColumn(SplitBillBtn, 0);
            Grid.SetColumnSpan(SplitBillBtn, 1);
            Grid.SetColumn(CloseOrderBtn, 1);
            Grid.SetColumnSpan(CloseOrderBtn, 2);
        }
        else
        {
            SplitBillBtn.IsVisible = false;
            CancelOrderBtn.IsVisible = true;
            Grid.SetColumn(CancelOrderBtn, 0);
            Grid.SetColumnSpan(CancelOrderBtn, 1);
            Grid.SetColumn(CloseOrderBtn, 2);
            Grid.SetColumnSpan(CloseOrderBtn, 1);
        }
    }

    private void BuildCategoryButtons()
    {
        if (_categoryBtnMap.Count == 0)
        {
            CategoryButtons.Children.Clear();
            foreach (var cat in _vm.Categories)
            {
                var btn = new Button
                {
                    Text = cat,
                    BorderColor = AppColors.CategoryBorder,
                    BorderWidth = 1,
                    CornerRadius = 22,
                    FontSize = 15,
                    Padding = new Thickness(20, 0),
                    HeightRequest = 48
                };
                btn.Clicked += (s, e) =>
                {
                    _vm.FilterByCategory(cat);
                    UpdateCategoryButtonColors();
                    ApplyCollectionViewFilter(); // D-1: sadece yeni source ver, rebuild yok
                };
                _categoryBtnMap[cat] = btn;
                CategoryButtons.Children.Add(btn);
            }
        }
        UpdateCategoryButtonColors();
    }

    private void UpdateCategoryButtonColors()
    {
        foreach (var (cat, btn) in _categoryBtnMap)
        {
            var isActive = cat == _vm.SelectedCategory;
            btn.BackgroundColor = isActive ? AppColors.Header : AppColors.Background;
            btn.TextColor = isActive ? Colors.White : AppColors.TextMuted;
        }
    }

    /// <summary>
    /// D-1: CollectionView.ItemsSource'u filtreli MenuItemViewModel listesiyle güncelle.
    /// Kategori değişiminde MAUI'nin CollectionView diff algoritması çalışır —
    /// sadece görünen öğeler render edilir (virtual scroll).
    /// Eski FlexLayout+kod-behind rebuild tamamen kaldırıldı.
    /// </summary>
    private void ApplyCollectionViewFilter()
    {
        var filteredVMs = _vm.FilteredMenuItems
            .Select(m => new MenuItemViewModel(m, _vm))
            .ToList();

        MenuCollectionView.ItemsSource = filteredVMs;
    }

    /// <summary>
    /// D-1: ViewModel event'inden gelen item ekleme bildirimi.
    /// Sepet panelini günceller — CollectionView'a dokunmaz.
    /// </summary>
    private void OnMenuItemAdded(MenuItemModel item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existingInOrder = _vm.OrderItems.FirstOrDefault(i => i.MenuItemId == item.Id);
            if (existingInOrder != null && _orderRowMap.ContainsKey(item.Id))
                RefreshOrderItemRow(existingInOrder);
            else if (existingInOrder != null)
                AddOrderItemRow(existingInOrder);
            UpdateTotal();
        });
    }

    private void BuildOrderItems()
    {
        OrderItemsLayout.Children.Clear();
        _orderRowMap.Clear();

        if (_vm.OrderItems.Count == 0)
        {
            OrderItemsLayout.Children.Add(CreateEmptyLabel());
            return;
        }

        foreach (var item in _vm.OrderItems)
            AddOrderItemRow(item);
    }

    private void AddOrderItemRow(OrderItemModel item)
    {
        var emptyLabel = OrderItemsLayout.Children.OfType<Label>().FirstOrDefault();
        if (emptyLabel != null) OrderItemsLayout.Children.Remove(emptyLabel);

        var menuItem = _vm.MenuItemLookup.GetValueOrDefault(item.MenuItemId);
        var name = menuItem?.Name ?? "Ürün";

        Grid grid;
        Label detailLabel;

        if (_isReadOnly)
        {
            grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(60)),
                    new ColumnDefinition(new GridLength(90))
                },
                Padding = new Thickness(16, 14),
                BackgroundColor = Colors.White
            };
            grid.Children.Add(new Label
            {
                Text = name,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.TextMain,
                VerticalOptions = LayoutOptions.Center
            });
            var qtyLabel = new Label
            {
                Text = $"x{item.Quantity}",
                FontSize = 15,
                TextColor = AppColors.TextMuted,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(qtyLabel, 1);
            grid.Children.Add(qtyLabel);
            detailLabel = new Label
            {
                Text = $"₺{item.Price * item.Quantity:F0}",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.Accent,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(detailLabel, 2);
            grid.Children.Add(detailLabel);
        }
        else
        {
            grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Padding = new Thickness(14, 10)
            };
            var infoStack = new VerticalStackLayout { Spacing = 3 };
            infoStack.Children.Add(new Label
            {
                Text = name,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.TextMain
            });
            detailLabel = new Label { Text = FormatDetail(item), FontSize = 13, TextColor = AppColors.Accent };
            infoStack.Children.Add(detailLabel);
            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var removeBtn = new Button
            {
                Text = "−",
                BackgroundColor = AppColors.TableFull,
                TextColor = AppColors.Danger,
                BorderColor = AppColors.TableFullBorder,
                BorderWidth = 1,
                CornerRadius = 10,
                WidthRequest = 44,
                HeightRequest = 44,
                FontSize = 22,
                Padding = new Thickness(0),
                VerticalOptions = LayoutOptions.Center
            };
            removeBtn.Clicked += async (s, e) =>
            {
                removeBtn.IsEnabled = false;
                var prevQty = item.Quantity;
                await _vm.RemoveItemAsync(item);
                if (prevQty > 1) RefreshOrderItemRow(item);
                else RemoveOrderItemRow(item);
                UpdateTotal();
            };
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(removeBtn);
        }

        var separator = new BoxView
        {
            Color = AppColors.CardBorder,
            HeightRequest = 1,
            Margin = new Thickness(8, 0)
        };

        OrderItemsLayout.Children.Add(grid);
        OrderItemsLayout.Children.Add(separator);
        _orderRowMap[item.MenuItemId] = (grid, detailLabel, separator);
    }

    private void RefreshOrderItemRow(OrderItemModel item)
    {
        if (_orderRowMap.TryGetValue(item.MenuItemId, out var row))
            row.DetailLabel.Text = _isReadOnly
                ? $"₺{item.Price * item.Quantity:F0}"
                : FormatDetail(item);
    }

    private void RemoveOrderItemRow(OrderItemModel item)
    {
        if (!_orderRowMap.TryGetValue(item.MenuItemId, out var row)) return;
        OrderItemsLayout.Children.Remove(row.Row);
        OrderItemsLayout.Children.Remove(row.Separator);
        _orderRowMap.Remove(item.MenuItemId);
        if (_orderRowMap.Count == 0)
            OrderItemsLayout.Children.Add(CreateEmptyLabel());
    }

    private static string FormatDetail(OrderItemModel item) =>
        $"₺{item.Price:F0} x {item.Quantity} = ₺{item.Price * item.Quantity:F0}";

    private static Label CreateEmptyLabel() => new Label
    {
        Text = "Henüz ürün eklenmedi",
        TextColor = AppColors.TextMuted,
        FontSize = 13,
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, 20)
    };

    private void UpdateTotal()
    {
        TotalLabel.Text = $"₺{_vm.Total:F0}";
    }

    private async void OnSplitBillClicked(object sender, EventArgs e)
    {
        if (_vm.OrderItems.Count == 0)
        {
            await DisplayAlert("Uyarı", "Sipariş boş, hesap bölünemez.", "Tamam");
            return;
        }

        // F-10: MenuItemLookup direkt geçiliyor
        var splitPage = new SplitBillPage(_vm.OrderItems.ToList(), _vm.MenuItemLookup);
        await Navigation.PushModalAsync(splitPage);
        await splitPage.WaitForCloseAsync();

        if (splitPage.PaidQuantities == null || splitPage.PaidQuantities.Count == 0)
            return;

        var toRemove = new List<OrderItemModel>();
        var toUpdate = new List<(OrderItemModel Item, int Remaining)>();

        foreach (var kv in splitPage.PaidQuantities)
        {
            var item = _vm.OrderItems.FirstOrDefault(i => i.Id == kv.Key);
            if (item == null) continue;
            int remaining = item.Quantity - kv.Value;
            if (remaining <= 0) toRemove.Add(item);
            else toUpdate.Add((item, remaining));
        }

        foreach (var item in toRemove) _vm.OrderItems.Remove(item);
        foreach (var (item, remaining) in toUpdate) item.Quantity = remaining;

        _vm.RecalcTotal();
        BuildOrderItems();
        UpdateTotal();

        // OnAppearing PopModalAsync animasyonu bitince tetiklenir — o anda DB yazımı
        // henüz bitmemiş olabilir. Flag ile reload engellenir, UI zaten doğru durumda.
        _skipNextReload = true;

        if (_vm.OrderItems.Count == 0)
        {
            await _vm.CloseOrderAsync();
            try { await Task.WhenAll(toRemove.Select(i => _vm.RemoveItemDbOnlyAsync(i))); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SplitBill DB hata: {ex.Message}"); }
            _skipNextReload = false;
            await Navigation.PopAsync();
            return;
        }

        try
        {
            await Task.WhenAll(
                toRemove.Select(i => _vm.RemoveItemDbOnlyAsync(i)).Concat(
                toUpdate.Select(t => _vm.SetItemQuantityAsync(t.Item, t.Remaining))));
        }
        catch (Exception ex)
        {
            _skipNextReload = false;
            await DisplayAlert("Hata", $"Hesap bölme kaydedilemedi: {ex.Message}", "Tamam");
            await _vm.ReloadOrderItemsAsync();
            BuildOrderItems();
            UpdateTotal();
        }
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnCloseOrderClicked(object sender, EventArgs e)
    {
        if (_vm.OrderItems.Count == 0)
        {
            await DisplayAlert("Uyarı", "Sipariş boş, hesap kapatılamaz.", "Tamam");
            return;
        }
        bool confirm = await DisplayAlert("Hesabı Kapat",
            $"Toplam: ₺{_vm.Total:F0}\nHesabı kapatmak istiyor musunuz?", "Evet, Kapat", "İptal");
        if (!confirm) return;
        await _vm.CloseOrderAsync();
        await Navigation.PopAsync();
    }

    private async void OnCancelOrderClicked(object sender, EventArgs e)
    {
        if (_vm.CurrentOrder == null && _vm.OrderItems.Count == 0)
        {
            await Navigation.PopAsync();
            return;
        }
        bool confirm = await DisplayAlert("Siparişi İptal Et",
            "Tüm siparişi iptal etmek istiyor musunuz?", "Evet, İptal Et", "Hayır");
        if (!confirm) return;
        await _vm.CloseOrderAsync();
        await Navigation.PopAsync();
    }
}