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
    private readonly ILogger<HrApiClient> _logger;

    public HrApiClient(HttpClient httpClient, IOptions<HrApiOptions> options, ILogger<HrApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
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

        var batches = Chunk(logs, _options.BatchSize);
        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = batch.Select(log => new
            {
                employeeId = log.EmployeeId,
                punchTime = log.PunchTime,
                punchType = log.PunchType,
                verifyMode = log.VerifyMode,
                workCode = log.WorkCode
            }).ToArray();

            _logger.LogInformation("Posting {Count} logs to HR API", payload.Length);
            using var response = await _httpClient.PostAsJsonAsync(_options.AttendanceEndpoint, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"HR API responded with {(int)response.StatusCode} - {response.ReasonPhrase}: {body}");
            }
        }
    }

    public async Task SyncEmployeesAsync(IReadOnlyCollection<Employee> employees, CancellationToken cancellationToken)
    {
        if (employees.Count == 0)
        {
            return;
        }

        var payload = employees.Select(e => new
        {
            employeeId = e.EmployeeId,
            name = e.Name,
            enabled = e.Enabled,
            privilege = e.Privilege
        }).ToArray();

        _logger.LogInformation("Syncing {Count} employee(s) to HR API", payload.Length);

        using var response = await _httpClient.PostAsJsonAsync("/api/employees/sync", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"HR API responded with {(int)response.StatusCode} - {response.ReasonPhrase}: {body}");
        }

        _logger.LogInformation("Successfully synced {Count} employee(s)", payload.Length);
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
