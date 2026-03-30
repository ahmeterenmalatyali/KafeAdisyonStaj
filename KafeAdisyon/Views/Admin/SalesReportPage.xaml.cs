using KafeAdisyon.Application.Interfaces;

namespace KafeAdisyon.Views.Admin;

/// <summary>
/// Admin panelinden açılan rapor ekranı.
/// Kullanıcı tarih aralığı seçer → "PDF Oluştur" → URL gösterilir.
/// </summary>
public partial class SalesReportPage : ContentPage
{
    private readonly ISalesReportService _reportService;

    // Seçili tarih aralığı
    private DateTime _from;
    private DateTime _to;
    private string? _lastUrl;

    public SalesReportPage(ISalesReportService reportService)
    {
        InitializeComponent();
        _reportService = reportService;

        // Varsayılan: bugün
        SelectToday();
        EntryTitle.Text = $"{DateTime.Now:dd MMMM yyyy} Satış Raporu";
    }

    // ── Tarih Aralığı Seçimi ───────────────────────────────────────

    private void OnRangeSelected(object sender, EventArgs e)
    {
        if (sender is not Button clicked) return;

        if (clicked == BtnToday)
            SelectToday();
        else
            SelectThisWeek();
    }

    private void SelectToday()
    {
        _from = DateTime.UtcNow.Date;
        _to = _from.AddDays(1).AddSeconds(-1);

        LblDateRange.Text = $"Bugün — {_from:dd.MM.yyyy}";

        // AGENTS.md renk paleti
        BtnToday.BackgroundColor = AppColors.Header;
        BtnToday.TextColor = Colors.White;
        BtnThisWeek.BackgroundColor = Colors.White;
        BtnThisWeek.TextColor = AppColors.TextMuted;
    }

    private void SelectThisWeek()
    {
        var today = DateTime.UtcNow.Date;
        var weekDay = (int)today.DayOfWeek;
        _from = today.AddDays(-(weekDay == 0 ? 6 : weekDay - 1)); // Pazartesi
        _to = _from.AddDays(7).AddSeconds(-1);

        LblDateRange.Text = $"Bu hafta — {_from:dd.MM.yyyy} - {_to:dd.MM.yyyy}";

        BtnThisWeek.BackgroundColor = AppColors.Header;
        BtnThisWeek.TextColor = Colors.White;
        BtnToday.BackgroundColor = Colors.White;
        BtnToday.TextColor = AppColors.TextMuted;
    }

    // ── Rapor Oluştur ──────────────────────────────────────────────

    private async void OnGenerateClicked(object sender, EventArgs e)
    {
        var title = EntryTitle.Text?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            await DisplayAlert("Uyarı", "Lütfen bir rapor başlığı girin.", "Tamam");
            return;
        }

        SetLoading(true);
        HideResultCards();

        var result = await _reportService.GenerateAndUploadReportAsync(_from, _to, title);

        SetLoading(false);

        if (result.Success && result.Data != null)
        {
            _lastUrl = result.Data;
            LblResultInfo.Text = $"Rapor başarıyla oluşturuldu.\n{_lastUrl}";
            ResultCard.IsVisible = true;
        }
        else
        {
            LblError.Text = $"❌ {result.Message}";
            ErrorCard.IsVisible = true;
        }
    }

    // ── Bağlantı Aksiyonları ───────────────────────────────────────

    private async void OnOpenLinkClicked(object sender, EventArgs e)
    {
        if (_lastUrl is null) return;
        await Launcher.OpenAsync(_lastUrl);
    }

    private async void OnCopyLinkClicked(object sender, EventArgs e)
    {
        if (_lastUrl is null) return;
        await Clipboard.SetTextAsync(_lastUrl);
        await DisplayAlert("Kopyalandı", "İndirme bağlantısı panoya kopyalandı.", "Tamam");
    }

    // ── Yardımcı Metodlar ─────────────────────────────────────────

    private void SetLoading(bool loading)
    {
        Spinner.IsRunning = loading;
        Spinner.IsVisible = loading;
        BtnGenerate.IsEnabled = !loading;
        BtnGenerate.Text = loading ? "Oluşturuluyor..." : "📄 PDF Oluştur ve Yükle";
    }

    private void HideResultCards()
    {
        ResultCard.IsVisible = false;
        ErrorCard.IsVisible = false;
    }
}