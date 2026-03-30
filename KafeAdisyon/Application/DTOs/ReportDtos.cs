namespace KafeAdisyon.Application.DTOs;

/// <summary>
/// PDF raporuna girecek tek bir sipariş satırı.
/// </summary>
public class OrderSummaryDto
{
    public string TableName { get; set; } = string.Empty;
    public DateTime ClosedAt { get; set; }
    public double Total { get; set; }
    public List<OrderItemSummaryDto> Items { get; set; } = new();
}

/// <summary>
/// Sipariş içindeki her ürün kalemi.
/// </summary>
public class OrderItemSummaryDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double LineTotal => Quantity * UnitPrice;
}

/// <summary>
/// Tüm raporun özet istatistikleri — PDF'in üst kısmında gösterilir.
/// </summary>
public class ReportSummaryDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int OrderCount { get; set; }
    public double GrandTotal { get; set; }

    /// <summary>En çok satan ürün adı ve satış adedi.</summary>
    public string TopProduct { get; set; } = "-";
    public int TopProductQty { get; set; }

    public List<OrderSummaryDto> Orders { get; set; } = new();
}