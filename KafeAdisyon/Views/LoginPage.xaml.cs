using KafeAdisyon.Views.Admin;
using KafeAdisyon.Views.Waiter;
using KafeAdisyon.ViewModels;

namespace KafeAdisyon.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnAdminClicked(object sender, EventArgs e)
    {
        var vm = Handler.MauiContext!.Services.GetService<AdminViewModel>()!;
        await Navigation.PushAsync(new AdminPage(vm));
    }

    private async void OnWaiterClicked(object sender, EventArgs e)
    {
        var vm = Handler.MauiContext!.Services.GetService<AdminViewModel>()!;
        await Navigation.PushAsync(new WaiterPage(vm));
    }
}