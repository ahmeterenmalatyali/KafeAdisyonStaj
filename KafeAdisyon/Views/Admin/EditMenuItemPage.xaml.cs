using KafeAdisyon.Models;

namespace KafeAdisyon.Views.Admin;

public partial class EditMenuItemPage : ContentPage
{
    public MenuItemModel? Result { get; private set; }

    private readonly MenuItemModel _item;
    private readonly List<string> _categories;
    private string _selectedCategory;

    private readonly TaskCompletionSource _tcs = new();
    public Task WaitForCloseAsync() => _tcs.Task;

    // F-01 benzeri: kategori butonları bir kez yaratılır, renk toggle ile güncellenir
    private readonly Dictionary<string, Button> _catBtnMap = new();

    public EditMenuItemPage(MenuItemModel item, List<string> categories)
    {
        InitializeComponent();
        _item = item;
        _categories = categories;
        _selectedCategory = item.Category;

        NameEntry.Text = item.Name;
        PriceEntry.Text = item.Price.ToString("F0");

        BuildCategoryButtons(); // Sadece constructor'da bir kez çalışır
    }

    /// <summary>
    /// F-01 / EditMenuItemPage: Kategori butonları bir kez oluşturulur.
    /// Seçim değişince tüm butonlar yeniden yaratılmaz — sadece renkleri güncellenir.
    /// </summary>
    private void BuildCategoryButtons()
    {
        CategoryButtonsLayout.Children.Clear();
        _catBtnMap.Clear();

        foreach (var cat in _categories)
        {
            var btn = new Button
            {
                Text = cat,
                // F-03: AppColors statik cache
                BackgroundColor = cat == _selectedCategory ? AppColors.Header     : Colors.White,
                TextColor       = cat == _selectedCategory ? Colors.White         : AppColors.Header,
                BorderColor     = cat == _selectedCategory ? AppColors.Header     : AppColors.InputBorder,
                BorderWidth = 1.5,
                CornerRadius = 20,
                FontSize = 13,
                Padding = new Thickness(14, 6),
                HeightRequest = 36,
                Margin = new Thickness(0, 0, 8, 8)
            };
            btn.Clicked += (s, e) =>
            {
                _selectedCategory = cat;
                UpdateCategoryButtonColors(); // Rebuild yok — sadece renk güncelle
            };
            _catBtnMap[cat] = btn;
            CategoryButtonsLayout.Children.Add(btn);
        }
    }

    /// <summary>
    /// Kategori seçilince sadece buton renklerini günceller — Children.Clear() yok.
    /// </summary>
    private void UpdateCategoryButtonColors()
    {
        foreach (var (cat, btn) in _catBtnMap)
        {
            var isSelected = cat == _selectedCategory;
            // F-03: AppColors statik cache
            btn.BackgroundColor = isSelected ? AppColors.Header    : Colors.White;
            btn.TextColor       = isSelected ? Colors.White        : AppColors.Header;
            btn.BorderColor     = isSelected ? AppColors.Header    : AppColors.InputBorder;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        var priceText = PriceEntry.Text?.Replace(",", ".");

        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("Hata", "Ürün adı boş olamaz.", "Tamam");
            return;
        }
        if (!double.TryParse(priceText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double price) || price <= 0)
        {
            await DisplayAlert("Hata", "Geçerli bir fiyat girin.", "Tamam");
            return;
        }

        _item.Name = name;
        _item.Category = _selectedCategory;
        _item.Price = price;
        Result = _item;

        await Navigation.PopModalAsync();
        _tcs.TrySetResult();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult();
    }

    private async void OnBackdropClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult();
    }
}
