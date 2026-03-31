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
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ─── Konfigürasyon ───────────────────────────────────────────────────
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

        // Offline altyapı
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<OfflineQueue>();

        // ─── Auth & Session ──────────────────────────────────────────────────
        // SessionContext Singleton — tüm uygulama boyunca tek örnek
        builder.Services.AddSingleton<SessionContext>();
        builder.Services.AddTransient<IAuthService, AuthService>();

        // ─── Audit Log ───────────────────────────────────────────────────────
        // Singleton: her servis aynı örneği kullanır, log sırası korunur
        builder.Services.AddSingleton<IAuditLogService, AuditLogService>();

        // ─── Sales Report ────────────────────────────────────────────────────
        builder.Services.AddSingleton<SupabaseStorageService>();
        builder.Services.AddTransient<ISalesReportService, SalesReportService>();
        builder.Services.AddTransient<SalesReportPage>();

        // ─── Services ────────────────────────────────────────────────────────
        builder.Services.AddTransient<ITableService, TableService>();

        // MenuService artık IAuditLogService'e bağımlı
        builder.Services.AddTransient<IMenuService, MenuService>();

        // OrderService artık IAuditLogService'e bağımlı
        // Somut tip olarak da kayıt gerekli — OfflineAwareOrderService direkt inject eder
        builder.Services.AddTransient<OrderService>();
        builder.Services.AddTransient<IOrderService, OfflineAwareOrderService>();

        // ─── ViewModels ──────────────────────────────────────────────────────
        builder.Services.AddSingleton<AdminViewModel>();

        // ─── Views ───────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<AdminPage>();
        builder.Services.AddTransient<WaiterPage>();
        builder.Services.AddTransient<AuditLogPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}