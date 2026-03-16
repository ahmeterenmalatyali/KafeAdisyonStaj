using KafeAdisyon.ViewModels;

namespace KafeAdisyon.Views;

/// <summary>
/// AdminPage ve WaiterPage'in ortak masa yönetimi kodunu barındırır.
/// </summary>
public abstract class TablePageBase : ContentPage
{
    protected readonly AdminViewModel Vm;

    protected static readonly Dictionary<string, string> BtnNames = new()
    {
        {"A-1","BtnA1"},{"A-2","BtnA2"},{"A-3","BtnA3"},{"A-4","BtnA4"},
        {"B-1","BtnB1"},{"B-2","BtnB2"},{"B-3","BtnB3"},{"B-4","BtnB4"},{"B-5","BtnB5"},
        {"C-1","BtnC1"},{"C-2","BtnC2"},{"C-3","BtnC3"},{"C-4","BtnC4"},{"C-5","BtnC5"}
    };

    // F-05: FindByName cache — constructor'da doldurulur, sonraki her çağrı O(1)
    private Dictionary<string, Button>? _tableBtnCache;

    protected TablePageBase(AdminViewModel vm)
    {
        Vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Vm.Tables.Count > 0)
            UpdateTableColors();

        if (Vm.Tables.Count == 0)
            await Vm.LoadDataAsync();
        else
            await Vm.RefreshTablesAsync();

        UpdateTableColors();
    }

    /// <summary>
    /// F-05: FindByName visual tree traversal → Dictionary cache.
    /// İlk çağrıda doldurulur (InitializeComponent sonrası), sonraki çağrılar O(1).
    /// </summary>
    protected void UpdateTableColors()
    {
        // Lazy init: InitializeComponent çağrıldıktan sonra butonlar hazır olur
        if (_tableBtnCache == null)
        {
            _tableBtnCache = new Dictionary<string, Button>(BtnNames.Count);
            foreach (var (tableName, btnName) in BtnNames)
            {
                var btn = this.FindByName<Button>(btnName);
                if (btn != null)
                    _tableBtnCache[tableName] = btn;
            }
        }

        foreach (var (tableName, btn) in _tableBtnCache)
        {
            var table = Vm.GetTableByName(tableName);
            if (table == null) continue;
            var isDolu = table.Status == "dolu";
            // F-03: AppColors statik cache — string parse yok
            btn.BackgroundColor = isDolu ? AppColors.TableFull  : AppColors.TableEmpty;
            btn.BorderColor     = isDolu ? AppColors.TableFullBorder : AppColors.TableEmptyBorder;
        }
    }

    protected async void OnLogoutClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
