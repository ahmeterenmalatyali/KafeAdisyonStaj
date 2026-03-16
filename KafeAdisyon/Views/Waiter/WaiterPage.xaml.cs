using KafeAdisyon.ViewModels;
using KafeAdisyon.Views.Order;

namespace KafeAdisyon.Views.Waiter;

public partial class WaiterPage : TablePageBase
{
    public WaiterPage(AdminViewModel vm) : base(vm)
    {
        InitializeComponent();
    }

    // OnAppearing, UpdateTableColors, BtnNames, OnLogoutClicked → TablePageBase'den gelir

    private async void OnTableClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        var tableName = btn.CommandParameter?.ToString();
        if (string.IsNullOrEmpty(tableName)) return;
        var table = Vm.GetTableByName(tableName);
        var tableId = table?.Id ?? tableName;
        await Navigation.PushAsync(new OrderPage(tableId, tableName, isReadOnly: false));
    }
}