namespace KafeAdisyon.Application.DTOs.RequestModels;

public class UpdateTableStatusRequest
{
    public string TableId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
