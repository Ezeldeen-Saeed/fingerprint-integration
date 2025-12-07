using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public interface IHrApiClient
{
    Task SendAsync(IReadOnlyCollection<AttendanceLog> logs, CancellationToken cancellationToken);
    Task SyncEmployeesAsync(IReadOnlyCollection<Employee> employees, CancellationToken cancellationToken);
}
