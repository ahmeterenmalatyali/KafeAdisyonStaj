using KafeAdisyon.Views;

namespace KafeAdisyon;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(LoginPage loginPage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(loginPage)
        {
            BarBackgroundColor = AppColors.NavBar,
            BarTextColor = Colors.White
        };
    }
}