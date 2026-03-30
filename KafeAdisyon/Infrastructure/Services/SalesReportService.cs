using KafeAdisyon.Application.DTOs;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Namespace çakışmalarını alias ile çözüyoruz
using QColors = QuestPDF.Helpers.Colors;
using QContainer = QuestPDF.Infrastructure.IContainer;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Günlük/haftalık satış özeti PDF raporu servisi.
///
/// Fabrika analojisi (32. gün gözlemi):
///   Fabrika  → Kantar verileri toplanır → Crystal Reports ile PDF → MinIO'ya yüklenir
///   Burada   → Supabase'den kapanan siparişler çekilir → QuestPDF ile PDF → Storage'a yüklenir
///
/// QuestPDF kurulum notu (.csproj'a ekle):
///   &lt;PackageReference Include="QuestPDF" Version="2024.10.0" /&gt;
///
/// QuestPDF Community lisansı — açık kaynak projeler için ücretsiz.
/// QuestPDF.Settings.License = LicenseType.Community; (MauiProgram.cs başında çağır)
/// </summary>
public class SalesReportService : ISalesReportService
{
    private readonly DatabaseClient _client;
    private readonly SupabaseStorageService _storage;

    public SalesReportService(DatabaseClient client, SupabaseStorageService storage)
    {
        _client = client;
        _storage = storage;
    }

    // ──────────────────────────────────────────────────────────────
    // ISalesReportService
    // ──────────────────────────────────────────────────────────────

    public async Task<BaseResponse<string>> GenerateAndUploadReportAsync(
        DateTime from, DateTime to, string reportTitle)
    {
        try
        {
            // 1) Veritabanından rapor verisini topla
            var summaryResult = await BuildReportSummaryAsync(from, to, reportTitle);
            if (!summaryResult.Success)
                return BaseResponse<string>.ErrorResult(summaryResult.Message);

            var summary = summaryResult.Data!;

            // 2) QuestPDF ile PDF byte dizisi üret
            var pdfBytes = GeneratePdf(summary);

            // 3) Supabase Storage'a yükle, public URL al
            var fileName = $"{from:yyyy-MM-dd}_{to:yyyy-MM-dd}_rapor.pdf";
            var uploadResult = await _storage.UploadPdfAsync(fileName, pdfBytes);

            return uploadResult;
        }
        catch (Exception ex)
        {
            return BaseResponse<string>.ErrorResult($"Rapor oluşturulamadı: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Veri Toplama
    // ──────────────────────────────────────────────────────────────

    private async Task<BaseResponse<ReportSummaryDto>> BuildReportSummaryAsync(
        DateTime from, DateTime to, string title)
    {
        try
        {
            // Belirtilen tarih aralığında kapanan siparişler
            var ordersResult = await _client.Db
                .Table<OrderModel>()
                .Select(DatabaseClient.OrderColumns + ",created_at")
                .Where(o => o.Status == "odendi")
                .Get();

            // Postgrest filtre operatörü — tarih aralığı
            var orders = ordersResult.Models
                .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
                .ToList();

            // Masa adlarını çek (id → name eşlemesi)
            var tablesResult = await _client.Db
                .Table<TableModel>()
                .Select(DatabaseClient.TableColumns)
                .Get();

            var tableMap = tablesResult.Models.ToDictionary(t => t.Id, t => t.Name);

            // Her sipariş için kalemleri çek
            var orderSummaries = new List<OrderSummaryDto>();
            var productCounter = new Dictionary<string, int>();

            // Menü ürün adlarını önceden yükle (id → name)
            var menuResult = await _client.Db
                .Table<MenuItemModel>()
                .Select(DatabaseClient.MenuItemColumns)
                .Get();
            var menuMap = menuResult.Models.ToDictionary(m => m.Id, m => m.Name);

            foreach (var order in orders)
            {
                var itemsResult = await _client.Db
                    .Table<OrderItemModel>()
                    .Select(DatabaseClient.OrderItemColumns)
                    .Where(i => i.OrderId == order.Id)
                    .Get();

                var itemDtos = itemsResult.Models.Select(i => new OrderItemSummaryDto
                {
                    ProductName = menuMap.GetValueOrDefault(i.MenuItemId, "Bilinmeyen Ürün"),
                    Quantity = i.Quantity,
                    UnitPrice = i.Price
                }).ToList();

                // En çok satan ürün sayacı
                foreach (var item in itemDtos)
                {
                    productCounter.TryGetValue(item.ProductName, out var current);
                    productCounter[item.ProductName] = current + item.Quantity;
                }

                orderSummaries.Add(new OrderSummaryDto
                {
                    TableName = tableMap.GetValueOrDefault(order.TableId, order.TableId),
                    ClosedAt = order.CreatedAt,
                    Total = order.Total,
                    Items = itemDtos
                });
            }

            // En çok satan ürün
            var top = productCounter
                .OrderByDescending(kv => kv.Value)
                .FirstOrDefault();

            var summary = new ReportSummaryDto
            {
                Title = title,
                From = from,
                To = to,
                OrderCount = orders.Count,
                GrandTotal = orders.Sum(o => o.Total),
                TopProduct = top.Key ?? "-",
                TopProductQty = top.Value,
                Orders = orderSummaries
            };

            return BaseResponse<ReportSummaryDto>.SuccessResult(summary);
        }
        catch (Exception ex)
        {
            return BaseResponse<ReportSummaryDto>.ErrorResult(
                $"Rapor verisi toplanamadı: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────
    // PDF Üretimi — QuestPDF Fluent API
    // Fabrika kantar raporuyla aynı mantık: şablon + veri → PDF byte[]
    // ──────────────────────────────────────────────────────────────

    private static byte[] GeneratePdf(ReportSummaryDto summary)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                // FIX: .Element() yerine doğrudan lambda zinciri kullanılıyor
                page.Header().Column(col => ComposeHeader(col, summary));
                page.Content().Column(col => ComposeContent(col, summary));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("KafeAdisyon — Üretildi: ");
                    text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).Bold();
                    text.Span("  |  Sayfa ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    // FIX: Action<IContainer> döndürmek yerine ColumnDescriptor direkt alıyor
    private static void ComposeHeader(ColumnDescriptor col, ReportSummaryDto s)
    {
        // Başlık çubuğu
        col.Item().Background("#3D2314").Padding(16).Row(row =>
        {
            row.RelativeItem().Text(s.Title)
                .FontSize(18).Bold().FontColor(QColors.White);
            row.ConstantItem(140).AlignRight().Text(
                $"{s.From:dd.MM.yyyy} – {s.To:dd.MM.yyyy}")
                .FontSize(10).FontColor("#A0856A");
        });

        // Özet kutuları
        col.Item().PaddingVertical(12).Row(row =>
        {
            SummaryBox(row, "Toplam Sipariş", s.OrderCount.ToString(), "#C8702A");
            SummaryBox(row, "Toplam Ciro", $"₺{s.GrandTotal:F2}", "#3D2314");
            SummaryBox(row, "En Çok Satan",
                $"{s.TopProduct} ({s.TopProductQty} adet)", "#5C3317");
        });
    }

    private static void SummaryBox(RowDescriptor row, string label, string value, string accent)
    {
        row.RelativeItem().Border(1).BorderColor("#E8D5C4").Padding(10).Column(col =>
        {
            col.Item().Text(label).FontSize(9).FontColor("#A0856A");
            col.Item().Text(value).Bold().FontSize(13).FontColor(accent);
        });
    }

    // FIX: Action<IContainer> döndürmek yerine ColumnDescriptor direkt alıyor
    private static void ComposeContent(ColumnDescriptor col, ReportSummaryDto s)
    {
        col.Spacing(10);

        foreach (var order in s.Orders)
        {
            // Sipariş kartı
            col.Item().Border(1).BorderColor("#E8D5C4")
                .Background("#FAF7F2").Padding(10).Column(card =>
                {
                    // Kart başlığı: Masa adı + saat + toplam
                    card.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"🪑 {order.TableName}")
                            .Bold().FontSize(11).FontColor("#3D2314");
                        row.ConstantItem(100).AlignCenter()
                            .Text(order.ClosedAt.ToLocalTime().ToString("HH:mm"))
                            .FontColor("#A0856A");
                        row.ConstantItem(80).AlignRight()
                            .Text($"₺{order.Total:F2}").Bold().FontColor("#C8702A");
                    });

                    // İnce çizgi
                    card.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#E8D5C4");

                    // Ürün tablosu
                    card.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3); // ürün adı
                            cols.RelativeColumn(1); // adet
                            cols.RelativeColumn(1); // birim fiyat
                            cols.RelativeColumn(1); // toplam
                        });

                        // Tablo başlığı
                        TableCell(table, "Ürün", header: true);
                        TableCell(table, "Adet", header: true);
                        TableCell(table, "Birim", header: true);
                        TableCell(table, "Toplam", header: true);

                        foreach (var item in order.Items)
                        {
                            TableCell(table, item.ProductName);
                            TableCell(table, item.Quantity.ToString());
                            TableCell(table, $"₺{item.UnitPrice:F2}");
                            TableCell(table, $"₺{item.LineTotal:F2}");
                        }
                    });
                });
        }

        // Hiç sipariş yoksa bilgi mesajı
        if (!s.Orders.Any())
        {
            col.Item().AlignCenter().Padding(30)
                .Text("Bu dönemde kapatılmış sipariş bulunmuyor.")
                .FontColor("#A0856A").FontSize(12);
        }
    }

    private static void TableCell(TableDescriptor table, string text, bool header = false)
    {
        var cell = table.Cell();

        // FIX: QContainer alias kullanılıyor — Microsoft.Maui.IContainer ile çakışma önlendi
        QContainer container = header
            ? cell.Background("#3D2314").Padding(6)
            : cell.BorderBottom(1).BorderColor("#F0E6D8").Padding(5);

        if (header)
            container.Text(text).Bold().FontColor(QColors.White).FontSize(9);
        else
            container.Text(text).FontColor("#2C1810").FontSize(9);
    }
}