using KafeAdisyon.Models;
using KafeAdisyon.ViewModels;
using KafeAdisyon.Views.Order;

namespace KafeAdisyon.Views.Admin;

public partial class AdminPage : TablePageBase
{
    private List<string> _menuCategories = new();
    private readonly Dictionary<string, Button> _menuCategoryBtnMap = new();

    // F-07: Menü listesi dirty flag — tab geçişinde gereksiz rebuild önlenir
    private bool _menuListDirty = true;

    public AdminPage(AdminViewModel vm) : base(vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnTableClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        var tableName = btn.CommandParameter?.ToString();
        if (string.IsNullOrEmpty(tableName)) return;
        var table = Vm.GetTableByName(tableName);
        var tableId = table?.Id ?? tableName;
        await Navigation.PushAsync(new OrderPage(tableId, tableName, isReadOnly: true));
    }

    // ── MENÜ ──────────────────────────────────────────

    private void BuildMenuTab()
    {
        _menuCategories = Vm.Categories.Where(c => c != MenuConstants.AllCategories).ToList();

        NewItemCategoryPicker.Items.Clear();
        foreach (var cat in _menuCategories)
            NewItemCategoryPicker.Items.Add(cat);

        BuildCategoryFilter();

        // F-07: Sadece dirty ise rebuild
        if (_menuListDirty)
        {
            BuildMenuList();
            _menuListDirty = false;
        }
        else
        {
            UpdateMenuCategoryButtonColors();
        }
    }

    private void OnNewItemCategoryPickerChanged(object sender, EventArgs e)
    {
        if (NewItemCategoryPicker.SelectedIndex >= 0)
            Vm.NewItemCategory = _menuCategories[NewItemCategoryPicker.SelectedIndex];
    }

    private void BuildCategoryFilter()
    {
        if (_menuCategoryBtnMap.Count == 0)
        {
            MenuCategoryButtons.Children.Clear();
            foreach (var cat in Vm.Categories)
            {
                var btn = new Button
                {
                    Text = cat,
                    // F-03: AppColors statik cache
                    BorderColor = AppColors.CategoryBorder,
                    BorderWidth = 1,
                    CornerRadius = 20,
                    FontSize = 13,
                    Padding = new Thickness(14, 6),
                    HeightRequest = 36
                };
                btn.Clicked += (s, ev) =>
                {
                    Vm.FilterByCategory(cat);
                    UpdateMenuCategoryButtonColors();
                    BuildMenuList();
                    _menuListDirty = false; // yeni build yapıldı
                };
                _menuCategoryBtnMap[cat] = btn;
                MenuCategoryButtons.Children.Add(btn);
            }
        }
        UpdateMenuCategoryButtonColors();
    }

    private void RebuildCategoryFilter()
    {
        _menuCategoryBtnMap.Clear();
        MenuCategoryButtons.Children.Clear();
        BuildCategoryFilter();
    }

    private void UpdateMenuCategoryButtonColors()
    {
        foreach (var (cat, btn) in _menuCategoryBtnMap)
        {
            var isActive = cat == Vm.SelectedCategory;
            // F-03: AppColors statik cache
            btn.BackgroundColor = isActive ? AppColors.Header     : AppColors.Background;
            btn.TextColor       = isActive ? Colors.White         : AppColors.TextMuted;
        }
    }

    private void BuildMenuList()
    {
        MenuListLayout.Children.Clear();
        foreach (var item in Vm.FilteredMenuItems)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(80)),
                    new ColumnDefinition(new GridLength(44)),
                    new ColumnDefinition(new GridLength(44))
                },
                Padding = new Thickness(14, 10),
                BackgroundColor = Colors.White
            };

            var nameStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            nameStack.Children.Add(new Label
            {
                Text = item.Name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.TextMain           // F-03
            });
            nameStack.Children.Add(new Label
            {
                Text = item.Category,
                FontSize = 11,
                TextColor = AppColors.TextMuted          // F-03
            });
            Grid.SetColumn(nameStack, 0);

            var priceLabel = new Label
            {
                Text = $"₺{item.Price:F0}",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.Accent,            // F-03
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(priceLabel, 1);

            var editBtn = new Button
            {
                Text = "✏️",
                BackgroundColor = AppColors.AccentLight, // F-03
                TextColor = AppColors.Accent,
                BorderColor = AppColors.AccentBorder,
                BorderWidth = 1,
                CornerRadius = 10,
                WidthRequest = 40,
                HeightRequest = 40,
                FontSize = 14,
                Padding = new Thickness(0),
                VerticalOptions = LayoutOptions.Center
            };
            editBtn.Clicked += async (s, e) =>
            {
                var editPage = new EditMenuItemPage(item, _menuCategories);
                await Navigation.PushModalAsync(editPage);
                await editPage.WaitForCloseAsync();

                if (editPage.Result != null)
                {
                    await Vm.UpdateMenuItemFullAsync(editPage.Result);
                    _menuListDirty = true; // F-07: sonraki tab açılışında rebuild
                    RebuildCategoryFilter();
                    BuildMenuList();
                    _menuListDirty = false;
                }
            };
            Grid.SetColumn(editBtn, 2);

            var deleteBtn = new Button
            {
                Text = "🗑️",
                BackgroundColor = AppColors.TableFull,   // F-03
                TextColor = AppColors.Danger,
                BorderColor = AppColors.TableFullBorder,
                BorderWidth = 1,
                CornerRadius = 10,
                WidthRequest = 40,
                HeightRequest = 40,
                FontSize = 14,
                Padding = new Thickness(0),
                VerticalOptions = LayoutOptions.Center
            };
            deleteBtn.Clicked += async (s, e) =>
            {
                bool confirm = await DisplayAlert("Sil",
                    $"{item.Name} silinsin mi?", "Evet", "Hayır");
                if (!confirm) return;
                await Vm.DeleteMenuItemAsync(item.Id);
                _menuListDirty = true; // F-07
                RebuildCategoryFilter();
                BuildMenuList();
                _menuListDirty = false;
            };
            Grid.SetColumn(deleteBtn, 3);

            grid.Children.Add(nameStack);
            grid.Children.Add(priceLabel);
            grid.Children.Add(editBtn);
            grid.Children.Add(deleteBtn);

            var border = new Border
            {
                Content = grid,
                BackgroundColor = Colors.White,
                StrokeThickness = 1,
                Stroke = AppColors.CardBorder,           // F-03
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Margin = new Thickness(0, 0, 0, 8)
            };
            MenuListLayout.Children.Add(border);
        }
    }

    private async void OnAddMenuItemClicked(object sender, EventArgs e)
    {
        if (NewItemCategoryPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Hata", "Lütfen bir kategori seçin.", "Tamam");
            return;
        }

        var success = await Vm.AddMenuItemAsync();
        if (success)
        {
            NewItemCategoryPicker.SelectedIndex = -1;
            _menuListDirty = true; // F-07
            RebuildCategoryFilter();
            BuildMenuList();
            _menuListDirty = false;
        }
        else
        {
            await DisplayAlert("Hata", "Lütfen tüm alanları doldurun.\nFiyat sayısal olmalı.", "Tamam");
        }
    }

    // ── TAB GEÇİŞLERİ ────────────────────────────────

    private void OnTabTables(object sender, EventArgs e)
    {
        TablesSection.IsVisible = true;
        MenuSection.IsVisible = false;
        // F-03: AppColors statik cache
        TabTables.BackgroundColor = AppColors.Header;
        TabTables.TextColor = Colors.White;
        TabMenu.BackgroundColor = Colors.White;
        TabMenu.TextColor = AppColors.TextMuted;
    }

    private void OnTabMenu(object sender, EventArgs e)
    {
        TablesSection.IsVisible = false;
        MenuSection.IsVisible = true;
        // F-03: AppColors statik cache
        TabMenu.BackgroundColor = AppColors.Header;
        TabMenu.TextColor = Colors.White;
        TabTables.BackgroundColor = Colors.White;
        TabTables.TextColor = AppColors.TextMuted;

        BuildMenuTab(); // F-07: dirty flag içinde kontrol edilir
    }
}
