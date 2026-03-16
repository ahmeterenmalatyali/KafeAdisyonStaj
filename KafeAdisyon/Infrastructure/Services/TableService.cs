using KafeAdisyon.Application.DTOs.RequestModels;
using KafeAdisyon.Application.Interfaces;
using KafeAdisyon.Common;
using KafeAdisyon.Infrastructure.Client;
using KafeAdisyon.Models;

namespace KafeAdisyon.Infrastructure.Services;

/// <summary>
/// Masa CRUD işlemlerinin implementasyonu.
/// AdministrativeAffairsService'deki DefinitionService yapısıyla aynı pattern.
/// </summary>
public class TableService : ITableService
{
    private readonly DatabaseClient _client;

    public TableService(DatabaseClient client)
    {
        _client = client;
    }

    public async Task<BaseResponse<List<TableModel>>> GetAllTablesAsync()
    {
        try
        {
            var result = await _client.Db
                .Table<TableModel>()
                .Select(DatabaseClient.TableColumns)
                .Get();

            return BaseResponse<List<TableModel>>.SuccessResult(
                result.Models, "Masalar başarıyla getirildi");
        }
        catch (Exception ex)
        {
            return BaseResponse<List<TableModel>>.ErrorResult($"Masalar getirilemedi: {ex.Message}");
        }
    }

    public async Task<BaseResponse<object>> UpdateTableStatusAsync(UpdateTableStatusRequest request)
    {
        try
        {
            await _client.Db
                .Table<TableModel>()
                .Where(t => t.Id == request.TableId)
                .Set(t => t.Status, request.Status)
                .Update();

            return BaseResponse<object>.SuccessResult(null, "Masa durumu güncellendi");
        }
        catch (Exception ex)
        {
            return BaseResponse<object>.ErrorResult($"Masa durumu güncellenemedi: {ex.Message}");
        }
    }
}
