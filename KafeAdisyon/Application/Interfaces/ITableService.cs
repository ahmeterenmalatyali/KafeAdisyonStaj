using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Common;
using KafeAdisyon.Models;

namespace KafeAdisyon.Application.Interfaces;

/// <summary>
/// Masa işlemleri için servis sözleşmesi.
/// </summary>
public interface ITableService
{
    Task<BaseResponse<List<TableModel>>> GetAllTablesAsync();
    Task<BaseResponse<object>> UpdateTableStatusAsync(UpdateTableStatusRequest request);
}
