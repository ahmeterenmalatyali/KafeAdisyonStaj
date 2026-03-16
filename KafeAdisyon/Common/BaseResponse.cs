namespace KafeAdisyon.Common;

/// <summary>
/// Tüm servis metodlarının dönüş tipi.
/// AdministrativeAffairsService'deki SharedKernel.BaseResponse ile aynı yapı.
/// </summary>
public class BaseResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static BaseResponse<T> SuccessResult(T? data, string message = "")
        => new() { Success = true, Data = data, Message = message };

    public static BaseResponse<T> ErrorResult(string message)
        => new() { Success = false, Message = message };
}
