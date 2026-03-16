namespace KafeAdisyon.Application.DTOs.RequestModels;

public class CloseOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string TableId { get; set; } = string.Empty;
    public double FinalTotal { get; set; }
}

public class AddOrderItemRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string MenuItemId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public double Price { get; set; }
}

public class UpdateOrderItemQuantityRequest
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
