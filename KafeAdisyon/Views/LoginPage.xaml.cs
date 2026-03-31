using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Infrastructure.Services;
using KafeAdisyon.ViewModels;
using KafeAdisyon.Views.Admin;
using KafeAdisyon.Views.Waiter;

namespace KafeAdisyon.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = EntryEmail.Text?.Trim() ?? string.Empty;
        var password = EntryPassword.Text ?? string.Empty;
        var device = EntryDevice.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(email) ||
            string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(device))
        {
            ShowError("Lütfen tüm alanları doldurun.");
            return;
        }

        SetLoading(true);

        var services = Handler.MauiContext!.Services;
        var authService = services.GetService<IAuthService>()!;
        var result = await authService.LoginAsync(email, password, device);

        SetLoading(false);

        if (!result.Success)
        {
            ShowError(result.Message);
            return;
        }

        // Rol bazlı yönlendirme
        var role = result.Data!;
        var reportService = services.GetService<ISalesReportService>()!;

        if (role == "admin")
        {
            var vm = services.GetService<AdminViewModel>()!;
            await Navigation.PushAsync(new AdminPage(vm, reportService));
        }
        else
        {
            var vm = services.GetService<AdminViewModel>()!;
            await Navigation.PushAsync(new WaiterPage(vm));
        }
    }

    private void ShowError(string message)
    {
        LblError.Text = message;
        LblError.IsVisible = true;
    }

    private void SetLoading(bool loading)
    {
        Spinner.IsRunning = loading;
        Spinner.IsVisible = loading;
        BtnLogin.IsEnabled = !loading;
        LblError.IsVisible = false;
    }
}