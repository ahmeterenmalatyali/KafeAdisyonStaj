using KafeAdisyon.Common;

namespace KafeAdisyon.Application.Interfaces;
public interface ISalesReportService
{
    Task<BaseResponse<string>> GenerateAndUploadReportAsync(
        DateTime from, DateTime to, string reportTitle);
}