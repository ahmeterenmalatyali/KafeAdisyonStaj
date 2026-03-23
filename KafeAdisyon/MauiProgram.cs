using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Infrastructure.Offline;
using KafeAdisyon.Infrastructure.Services;
using KafeAdisyon.ViewModels;
using KafeAdisyon.Views;
using KafeAdisyon.Views.Admin;
using KafeAdisyon.Views.Waiter;

namespace KafeAdisyon;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Önce env variable'lardan oku, yoksa appsettings.json fallback
        var envUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var envKey = Environment.GetEnvironmentVariable("SUPABASE_KEY");

        if (!string.IsNullOrWhiteSpace(envUrl) && !string.IsNullOrWhiteSpace(envKey))
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = envUrl,
                ["Supabase:PublishableKey"] = envKey
            });
        }
        else
        {
            using var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("KafeAdisyon.appsettings.json");
            if (stream != null)
                builder.Configuration.AddJsonStream(stream);
        }

        // ─── Infrastructure ──────────────────────────────────────────────────
        builder.Services.AddSingleton<DatabaseClient>();

        // Offline altyapı — Singleton (uygulama boyunca tek örnek)
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<OfflineQueue>();

        // ─── Services ────────────────────────────────────────────────────────
        // MAUI'de Scoped root container'dan resolve edilemez → Transient kullan

        builder.Services.AddTransient<ITableService, TableService>();
        builder.Services.AddTransient<IMenuService, MenuService>();

        // Asıl OrderService somut tip olarak da kaydedilmeli —
        // OfflineAwareOrderService constructor'ı OrderService'i direkt enjekte eder
        builder.Services.AddTransient<OrderService>();

        // IOrderService isteklerini OfflineAwareOrderService karşılar
        builder.Services.AddTransient<IOrderService, OfflineAwareOrderService>();

        // ─── ViewModels ──────────────────────────────────────────────────────
        builder.Services.AddSingleton<AdminViewModel>();

        // ─── Views ───────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<AdminPage>();
        builder.Services.AddTransient<WaiterPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}