using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Infrastructure.Services;
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

    // Rapor sekmesi için tarih aralığı ve son URL
    private DateTime _reportFrom;
    private DateTime _reportTo;
    private string? _lastReportUrl;

    private readonly ISalesReportService _reportService;

    public AdminPage(AdminViewModel vm, ISalesReportService reportService) : base(vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _reportService = reportService;

        // Rapor sekmesi varsayılanı: bugün
        SelectReportToday();
        EntryReportTitle.Text = $"{DateTime.Now:dd MMMM yyyy} Satış Raporu";
    }

    // ── MASALAR ───────────────────────────────────────

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
                    _menuListDirty = false;
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
            btn.BackgroundColor = isActive ? AppColors.Header : AppColors.Background;
            btn.TextColor = isActive ? Colors.White : AppColors.TextMuted;
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
                TextColor = AppColors.TextMain
            });
            nameStack.Children.Add(new Label
            {
                Text = item.Category,
                FontSize = 11,
                TextColor = AppColors.TextMuted
            });
            Grid.SetColumn(nameStack, 0);

            var priceLabel = new Label
            {
                Text = $"₺{item.Price:F0}",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppColors.Accent,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(priceLabel, 1);

            var editBtn = new Button
            {
                Text = "✏️",
                BackgroundColor = AppColors.AccentLight,
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
                    _menuListDirty = true;
                    RebuildCategoryFilter();
                    BuildMenuList();
                    _menuListDirty = false;
                }
            };
            Grid.SetColumn(editBtn, 2);

            var deleteBtn = new Button
            {
                Text = "🗑️",
                BackgroundColor = AppColors.TableFull,
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
                _menuListDirty = true;
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
                Stroke = AppColors.CardBorder,
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
            _menuListDirty = true;
            RebuildCategoryFilter();
            BuildMenuList();
            _menuListDirty = false;
        }
        else
        {
            await DisplayAlert("Hata", "Lütfen tüm alanları doldurun.\nFiyat sayısal olmalı.", "Tamam");
        }
    }

    // ── RAPOR ─────────────────────────────────────────

    private void OnRangeSelected(object sender, EventArgs e)
    {
        if (sender is not Button clicked) return;
        if (clicked == BtnToday) SelectReportToday();
        else SelectReportThisWeek();
    }

    private void SelectReportToday()
    {
        _reportFrom = DateTime.UtcNow.Date;
        _reportTo = _reportFrom.AddDays(1).AddSeconds(-1);
        LblDateRange.Text = $"Bugün — {_reportFrom:dd.MM.yyyy}";

        BtnToday.BackgroundColor = AppColors.Header;
        BtnToday.TextColor = Colors.White;
        BtnThisWeek.BackgroundColor = Colors.White;
        BtnThisWeek.TextColor = AppColors.TextMuted;
    }

    private void SelectReportThisWeek()
    {
        var today = DateTime.UtcNow.Date;
        var weekDay = (int)today.DayOfWeek;
        _reportFrom = today.AddDays(-(weekDay == 0 ? 6 : weekDay - 1));
        _reportTo = _reportFrom.AddDays(7).AddSeconds(-1);
        LblDateRange.Text = $"Bu hafta — {_reportFrom:dd.MM.yyyy} – {_reportTo:dd.MM.yyyy}";

        BtnThisWeek.BackgroundColor = AppColors.Header;
        BtnThisWeek.TextColor = Colors.White;
        BtnToday.BackgroundColor = Colors.White;
        BtnToday.TextColor = AppColors.TextMuted;
    }

    private async void OnGenerateReportClicked(object sender, EventArgs e)
    {
        var title = EntryReportTitle.Text?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            await DisplayAlert("Uyarı", "Lütfen bir rapor başlığı girin.", "Tamam");
            return;
        }

        SetReportLoading(true);
        HideReportCards();

        var result = await _reportService.GenerateAndUploadReportAsync(_reportFrom, _reportTo, title);

        SetReportLoading(false);

        if (result.Success && result.Data != null)
        {
            _lastReportUrl = result.Data;
            LblReportUrl.Text = _lastReportUrl;
            ReportResultCard.IsVisible = true;
        }
        else
        {
            LblReportError.Text = $"❌ {result.Message}";
            ReportErrorCard.IsVisible = true;
        }
    }

    private async void OnOpenReportLinkClicked(object sender, EventArgs e)
    {
        if (_lastReportUrl is null) return;
        await Launcher.OpenAsync(_lastReportUrl);
    }

    private async void OnCopyReportLinkClicked(object sender, EventArgs e)
    {
        if (_lastReportUrl is null) return;
        await Clipboard.SetTextAsync(_lastReportUrl);
        await DisplayAlert("Kopyalandı", "İndirme bağlantısı panoya kopyalandı.", "Tamam");
    }

    private void SetReportLoading(bool loading)
    {
        ReportSpinner.IsRunning = loading;
        ReportSpinner.IsVisible = loading;
        BtnGenerate.IsEnabled = !loading;
        BtnGenerate.Text = loading ? "Oluşturuluyor..." : "📄  PDF Oluştur ve Yükle";
    }

    private void HideReportCards()
    {
        ReportResultCard.IsVisible = false;
        ReportErrorCard.IsVisible = false;
    }

    // ── TAB GEÇİŞLERİ ────────────────────────────────

    private void OnTabTables(object sender, EventArgs e)
    {
        TablesSection.IsVisible = true;
        MenuSection.IsVisible = false;
        ReportSection.IsVisible = false;
        LogSection.IsVisible = false;

        TabTables.BackgroundColor = AppColors.Header;
        TabTables.TextColor = Colors.White;
        TabMenu.BackgroundColor = Colors.White;
        TabMenu.TextColor = AppColors.TextMuted;
        TabReport.BackgroundColor = Colors.White;
        TabReport.TextColor = AppColors.TextMuted;
    }

    private void OnTabMenu(object sender, EventArgs e)
    {
        TablesSection.IsVisible = false;
        MenuSection.IsVisible = true;
        ReportSection.IsVisible = false;
        LogSection.IsVisible = false;

        TabMenu.BackgroundColor = AppColors.Header;
        TabMenu.TextColor = Colors.White;
        TabTables.BackgroundColor = Colors.White;
        TabTables.TextColor = AppColors.TextMuted;
        TabReport.BackgroundColor = Colors.White;
        TabReport.TextColor = AppColors.TextMuted;

        BuildMenuTab(); // F-07: dirty flag içinde kontrol edilir
    }

    private void OnTabReport(object sender, EventArgs e)
    {
        TablesSection.IsVisible = false;
        MenuSection.IsVisible = false;
        ReportSection.IsVisible = true;
        LogSection.IsVisible = false;

        TabReport.BackgroundColor = AppColors.Header;
        TabReport.TextColor = Colors.White;
        TabTables.BackgroundColor = Colors.White;
        TabTables.TextColor = AppColors.TextMuted;
        TabMenu.BackgroundColor = Colors.White;
        TabMenu.TextColor = AppColors.TextMuted;
        TabLog.BackgroundColor = Colors.White;
        TabLog.TextColor = AppColors.TextMuted;
    }

    private void OnTabLog(object sender, EventArgs e)
    {
        TablesSection.IsVisible = false;
        MenuSection.IsVisible = false;
        ReportSection.IsVisible = false;
        LogSection.IsVisible = true;

        TabLog.BackgroundColor = AppColors.Header;
        TabLog.TextColor = Colors.White;
        TabTables.BackgroundColor = Colors.White;
        TabTables.TextColor = AppColors.TextMuted;
        TabMenu.BackgroundColor = Colors.White;
        TabMenu.TextColor = AppColors.TextMuted;
        TabReport.BackgroundColor = Colors.White;
        TabReport.TextColor = AppColors.TextMuted;
    }

    private async void OnOpenAuditLogClicked(object sender, EventArgs e)
    {
        var audit = Handler.MauiContext!.Services.GetService<IAuditLogService>()!;
        await Navigation.PushAsync(new AuditLogPage(audit));
    }
}