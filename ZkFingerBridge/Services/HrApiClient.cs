using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public sealed class HrApiClient : IHrApiClient
{
    private readonly HttpClient _httpClient;
    private readonly HrApiOptions _options;
    private readonly ZkDeviceOptions _deviceOptions;
    private readonly ILogger<HrApiClient> _logger;

    public HrApiClient(
        HttpClient httpClient, 
        IOptions<HrApiOptions> options, 
        IOptions<ZkDeviceOptions> deviceOptions,
        ILogger<HrApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _deviceOptions = deviceOptions.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var scheme = string.IsNullOrWhiteSpace(_options.AuthorizationScheme)
                ? "Bearer"
                : _options.AuthorizationScheme;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(scheme, _options.ApiKey);
        }
    }

    public async Task SendAsync(IReadOnlyCollection<AttendanceLog> logs, CancellationToken cancellationToken)
    {
        if (logs.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Sending {Count} logs to HR API at {Endpoint}", logs.Count, _options.AttendanceEndpoint);

        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build payload in Firstsoft.io PHP API format (single object, not array)
            var payload = new
            {
                company_id = _options.CompanyId,
                branch_id = _deviceOptions.BranchId,
                employee_id = log.EmployeeId,
                punch_time = log.PunchTime.ToString("yyyy-MM-dd HH:mm:ss"),
                verify_mode = log.VerifyMode,
                punch_type = log.PunchType,
                work_code = log.WorkCode
            };

            _logger.LogDebug("Posting log for employee {EmployeeId} at {PunchTime}", log.EmployeeId, payload.punch_time);
            using var response = await _httpClient.PostAsJsonAsync(_options.AttendanceEndpoint, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("HR API error for employee {EmployeeId}: {Status} - {Body}", log.EmployeeId, response.StatusCode, body);
                throw new InvalidOperationException($"HR API responded with {(int)response.StatusCode} - {response.ReasonPhrase}: {body}");
            }
            else
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("✅ Sent log for employee {EmployeeId}: {Result}", log.EmployeeId, result);
            }
        }
    }

    public async Task<CompaniesResponse?> GetCompaniesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching companies from {Endpoint}", _options.CompaniesEndpoint);
        
        try
        {
            var response = await _httpClient.GetFromJsonAsync<CompaniesResponse>(
                _options.CompaniesEndpoint, 
                cancellationToken);
            
            if (response?.Success == true)
            {
                _logger.LogInformation("Retrieved {Count} companies from API", response.Data?.Count ?? 0);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch companies from API");
            throw;
        }
    }

    private static IEnumerable<IReadOnlyCollection<AttendanceLog>> Chunk(IReadOnlyCollection<AttendanceLog> source, int size)
    {
        if (size <= 0)
        {
            yield return source;
            yield break;
        }

        var batch = new List<AttendanceLog>(size);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == size)
            {
                yield return batch.ToArray();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }
}
