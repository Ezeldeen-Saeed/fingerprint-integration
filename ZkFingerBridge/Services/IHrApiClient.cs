using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public interface IHrApiClient
{
    Task SendAsync(IReadOnlyCollection<AttendanceLog> logs, CancellationToken cancellationToken);
    
    /// <summary>
    /// Fetches the list of companies and their branches from the API
    /// </summary>
    Task<CompaniesResponse?> GetCompaniesAsync(CancellationToken cancellationToken);
}
