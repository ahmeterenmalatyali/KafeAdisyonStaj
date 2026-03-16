using KafeAdisyon.Models;

namespace KafeAdisyon.Views.Order;

public partial class SplitBillPage : ContentPage
{
    private readonly List<OrderItemModel> _orderItems;
    // F-10: List<MenuItemModel> yerine Dictionary direkt alınır — caller'da ToDictionary yok
    private readonly Dictionary<string, MenuItemModel> _menuItemLookup;
    private readonly Dictionary<string, int> _selectedQuantities = new();

    public Dictionary<string, int>? PaidQuantities { get; private set; }

    private readonly TaskCompletionSource _tcs = new();
    public Task WaitForCloseAsync() => _tcs.Task;

    private record RowRefs(Border Border, Label QtyLabel, Button MinusBtn, Button PlusBtn);
    private readonly Dictionary<string, RowRefs> _rowRefs = new();

    /// <summary>
    /// F-10: menuItemLookup Dictionary olarak alınır — içeride List→Dictionary dönüşümü yok.
    /// Caller (OrderPage) zaten _vm.MenuItemLookup'a sahip, ekstra kopya oluşturulmaz.
    /// </summary>
    public SplitBillPage(List<OrderItemModel> orderItems, Dictionary<string, MenuItemModel> menuItemLookup)
    {
        InitializeComponent();
        _orderItems = orderItems;
        _menuItemLookup = menuItemLookup;

        foreach (var item in orderItems)
            _selectedQuantities[item.Id] = 0;

        BuildItems();
    }

    private void BuildItems()
    {
        ItemsLayout.Children.Clear();
        _rowRefs.Clear();

        foreach (var item in _orderItems)
        {
            var menuItem = _menuItemLookup.GetValueOrDefault(item.MenuItemId);
            var name = menuItem?.Name ?? "Ürün";
            var selected = _selectedQuantities[item.Id];

            var border = new Border
            {
                BackgroundColor = Colors.White,
                StrokeThickness = 1.5,
                Stroke = AppColors.CardBorder,           // F-03
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 10)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                }
            };

            var nameStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            nameStack.Children.Add(new Label
            {
                Text = name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.TextMain           // F-03
            });
            nameStack.Children.Add(new Label
            {
                Text = $"₺{item.Price:F0} x {item.Quantity} adet (maks)",
                FontSize = 11,
                TextColor = AppColors.TextMuted          // F-03
            });
            Grid.SetColumn(nameStack, 0);

            var controlStack = new HorizontalStackLayout
            {
                Spacing = 8,
                VerticalOptions = LayoutOptions.Center
            };

            var minusBtn = new Button
            {
                Text = "−",
                BackgroundColor = AppColors.DisabledBg,  // F-03
                TextColor = AppColors.Disabled,          // F-03
                BorderWidth = 0,
                CornerRadius = 8,
                WidthRequest = 34,
                HeightRequest = 34,
                FontSize = 18,
                Padding = new Thickness(0),
                IsEnabled = false
            };

            var qtyLabel = new Label
            {
                Text = "0",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.TextMuted,         // F-03
                WidthRequest = 28,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var plusBtn = new Button
            {
                Text = "+",
                BackgroundColor = AppColors.Header,      // F-03
                TextColor = Colors.White,
                BorderWidth = 0,
                CornerRadius = 8,
                WidthRequest = 34,
                HeightRequest = 34,
                FontSize = 18,
                Padding = new Thickness(0),
                IsEnabled = true
            };

            minusBtn.Clicked += (s, e) =>
            {
                if (_selectedQuantities[item.Id] <= 0) return;
                _selectedQuantities[item.Id]--;
                UpdateRowVisuals(item);
                UpdateTotal();
            };

            plusBtn.Clicked += (s, e) =>
            {
                if (_selectedQuantities[item.Id] >= item.Quantity) return;
                _selectedQuantities[item.Id]++;
                UpdateRowVisuals(item);
                UpdateTotal();
            };

            controlStack.Children.Add(minusBtn);
            controlStack.Children.Add(qtyLabel);
            controlStack.Children.Add(plusBtn);
            Grid.SetColumn(controlStack, 1);

            grid.Children.Add(nameStack);
            grid.Children.Add(controlStack);
            border.Content = grid;
            ItemsLayout.Children.Add(border);

            _rowRefs[item.Id] = new RowRefs(border, qtyLabel, minusBtn, plusBtn);
        }

        UpdateTotal();
    }

    private void UpdateRowVisuals(OrderItemModel item)
    {
        if (!_rowRefs.TryGetValue(item.Id, out var refs)) return;
        var selected = _selectedQuantities[item.Id];

        refs.QtyLabel.Text = selected.ToString();
        // F-03: AppColors statik cache
        refs.QtyLabel.TextColor = selected > 0 ? AppColors.Accent : AppColors.TextMuted;

        refs.Border.BackgroundColor = selected > 0 ? AppColors.AccentLight : Colors.White;
        refs.Border.Stroke = selected > 0 ? AppColors.Accent : AppColors.CardBorder;

        refs.MinusBtn.IsEnabled = selected > 0;
        refs.MinusBtn.BackgroundColor = selected > 0 ? AppColors.TableFull    : AppColors.DisabledBg;
        refs.MinusBtn.TextColor       = selected > 0 ? AppColors.Danger       : AppColors.Disabled;

        refs.PlusBtn.IsEnabled = selected < item.Quantity;
        refs.PlusBtn.BackgroundColor = selected < item.Quantity ? AppColors.Header : AppColors.DisabledBg;
    }

    private void UpdateTotal()
    {
        double total = 0;
        foreach (var item in _orderItems)
            total += item.Price * _selectedQuantities[item.Id];
        SplitTotalLabel.Text = $"₺{total:F2}";

        PayBtn.IsEnabled = total > 0;
        // F-03: AppColors statik cache
        PayBtn.BackgroundColor = total > 0 ? AppColors.Header : AppColors.Disabled;
    }

    private async void OnPayClicked(object sender, EventArgs e)
    {
        var anySelected = _selectedQuantities.Any(kv => kv.Value > 0);
        if (!anySelected)
        {
            await DisplayAlert("Uyarı", "Hiç ürün seçilmedi.", "Tamam");
            return;
        }

        double total = _orderItems.Sum(i => i.Price * _selectedQuantities[i.Id]);

        bool confirm = await DisplayAlert(
            "Öde ve Kaldır",
            $"Seçilen ürünler: ₺{total:F2}\nBu ürünleri ödenmiş say ve sepetten kaldır?",
            "Evet", "İptal");

        if (!confirm) return;

        PaidQuantities = _selectedQuantities
            .Where(kv => kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        _tcs.TrySetResult();
        await Navigation.PopModalAsync();
    }

    private async void OnBackdropClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult();
        await Navigation.PopModalAsync();
    }
}
