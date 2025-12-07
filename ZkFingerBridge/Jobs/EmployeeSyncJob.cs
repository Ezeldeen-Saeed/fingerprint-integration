using Microsoft.Extensions.Options;
using Quartz;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Services;

namespace ZkFingerBridge.Jobs;

/// <summary>
/// Quartz job that synchronizes employee data from the ZKTeco device to the HR API.
/// Runs daily at 2am and also on application startup.
/// </summary>
[DisallowConcurrentExecution]
public class EmployeeSyncJob : IJob
{
    private readonly ILogger<EmployeeSyncJob> _logger;
    private readonly IZkDeviceClient _deviceClient;
    private readonly IHrApiClient _hrApiClient;
    private readonly ZkDeviceOptions _deviceOptions;

    public EmployeeSyncJob(
        ILogger<EmployeeSyncJob> logger,
        IZkDeviceClient deviceClient,
        IHrApiClient hrApiClient,
        IOptions<ZkDeviceOptions> deviceOptions)
    {
        _logger = logger;
        _deviceClient = deviceClient;
        _hrApiClient = hrApiClient;
        _deviceOptions = deviceOptions.Value;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        
        _logger.LogInformation("Starting employee sync from device {Machine}", _deviceOptions.MachineNumber);

        try
        {
            // Read all employees from the ZKTeco device
            var employees = await _deviceClient.ReadEmployeesAsync(cancellationToken);
            
            if (employees.Count == 0)
            {
                _logger.LogInformation("No employees found on device");
                return;
            }

            // Sync to HR API
            await _hrApiClient.SyncEmployeesAsync(employees, cancellationToken);
            
            _logger.LogInformation("Successfully synced {Count} employee(s)", employees.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync employees");
            throw; // Quartz will handle retry if configured
        }
    }
}
