namespace KafeAdisyon.Application.DTOs.RequestModels;

public class AddMenuItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Price { get; set; }
}

public class UpdateMenuItemRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsActive { get; set; } = true;
}
