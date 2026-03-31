using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Models;

namespace KafeAdisyon.Views.Admin;

public partial class AuditLogPage : ContentPage
{
    private readonly IAuditLogService _audit;

    // Aksiyon → emoji eşlemesi
    private static readonly Dictionary<string, string> ActionEmoji = new()
    {
        { "hesap_kapatma",    "💳" },
        { "siparis_iptali",   "❌" },
        { "fiyat_guncelleme", "💰" },
        { "urun_ekleme",      "➕" },
        { "urun_silme",       "🗑️" }
    };

    private static readonly Dictionary<string, string> ActionLabel = new()
    {
        { "hesap_kapatma",    "Hesap Kapatma" },
        { "siparis_iptali",   "Sipariş İptali" },
        { "fiyat_guncelleme", "Fiyat Güncelleme" },
        { "urun_ekleme",      "Ürün Ekleme" },
        { "urun_silme",       "Ürün Silme" }
    };

    public AuditLogPage(IAuditLogService audit)
    {
        InitializeComponent();
        _audit = audit;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLogsAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
        => await LoadLogsAsync();

    private async Task LoadLogsAsync()
    {
        Spinner.IsRunning = true;
        Spinner.IsVisible = true;
        LblEmpty.IsVisible = false;

        // Spinner + LblEmpty dışındaki tüm kartları temizle
        var toRemove = LogListLayout.Children
            .Where(c => c != Spinner && c != LblEmpty)
            .ToList();
        foreach (var c in toRemove)
            LogListLayout.Children.Remove(c);

        var result = await _audit.GetRecentLogsAsync(100);

        Spinner.IsRunning = false;
        Spinner.IsVisible = false;

        if (!result.Success || result.Data == null || result.Data.Count == 0)
        {
            LblEmpty.IsVisible = true;
            return;
        }

        foreach (var log in result.Data)
            LogListLayout.Children.Add(BuildLogCard(log));
    }

    private static View BuildLogCard(AuditLogModel log)
    {
        var emoji = ActionEmoji.GetValueOrDefault(log.Action, "📋");
        var label = ActionLabel.GetValueOrDefault(log.Action, log.Action);

        // Aksiyona göre vurgu rengi
        var accentColor = log.Action switch
        {
            "hesap_kapatma" => "#2E7D32",   // yeşil
            "siparis_iptali" => "#C62828",   // kırmızı
            "fiyat_guncelleme" => "#E65100",   // turuncu
            "urun_ekleme" => "#1565C0",   // mavi
            "urun_silme" => "#6A1B9A",   // mor
            _ => "#5C3317"
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(44)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(80))
            },
            ColumnSpacing = 10,
            Padding = new Thickness(12, 10)
        };

        // Emoji badge
        var badge = new Border
        {
            BackgroundColor = Color.FromArgb(accentColor + "22"), // %13 opaklık
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            WidthRequest = 40,
            HeightRequest = 40,
            VerticalOptions = LayoutOptions.Center
        };
        badge.Content = new Label
        {
            Text = emoji,
            FontSize = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(badge, 0);

        // Orta — aksiyon + detay + kullanıcı/cihaz
        var middle = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        middle.Children.Add(new Label
        {
            Text = label,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(accentColor)
        });
        middle.Children.Add(new Label
        {
            Text = log.Detail,
            FontSize = 12,
            TextColor = Color.FromArgb("#2C1810")
        });
        middle.Children.Add(new Label
        {
            Text = $"👤 {log.UserName}  ({log.Role})  📱 {log.DeviceName}",
            FontSize = 10,
            TextColor = Color.FromArgb("#A0856A")
        });
        Grid.SetColumn(middle, 1);

        // Sağ — tarih/saat
        var timeStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            Spacing = 2
        };
        var local = log.CreatedAt.ToLocalTime();
        timeStack.Children.Add(new Label
        {
            Text = local.ToString("dd.MM.yy"),
            FontSize = 10,
            TextColor = Color.FromArgb("#A0856A"),
            HorizontalOptions = LayoutOptions.End
        });
        timeStack.Children.Add(new Label
        {
            Text = local.ToString("HH:mm"),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5C3317"),
            HorizontalOptions = LayoutOptions.End
        });
        Grid.SetColumn(timeStack, 2);

        grid.Children.Add(badge);
        grid.Children.Add(middle);
        grid.Children.Add(timeStack);

        return new Border
        {
            Content = grid,
            BackgroundColor = Colors.White,
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E8D5C4"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Margin = new Thickness(0, 0, 0, 0)
        };
    }
}