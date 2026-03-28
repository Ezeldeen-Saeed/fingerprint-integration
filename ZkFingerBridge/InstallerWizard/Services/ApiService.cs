using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InstallerWizard.Services;

/// <summary>
/// API service for validating installation keys
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.firstsoft.io";

    public ApiService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZkFingerBridge-Installer/2.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<KeyValidationResult> ValidateKeyAsync(string installationKey)
    {
        try
        {
            var request = new { installationKey };
            var response = await _httpClient.PostAsJsonAsync("/api/installer/activate", request);

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ActivateKeyResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success == true)
                {
                    return new KeyValidationResult
                    {
                        Success = true,
                        Branch = result.Branch
                    };
                }

                return new KeyValidationResult
                {
                    Success = false,
                    Error = result?.Error ?? "Unknown error"
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new KeyValidationResult
                {
                    Success = false,
                    Error = "Invalid installation key"
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // Try to parse the error message
                try
                {
                    var result = JsonSerializer.Deserialize<ActivateKeyResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return new KeyValidationResult
                    {
                        Success = false,
                        Error = result?.Error ?? "This key has already been used"
                    };
                }
                catch
                {
                    return new KeyValidationResult
                    {
                        Success = false,
                        Error = "This key has already been used"
                    };
                }
            }
            else
            {
                return new KeyValidationResult
                {
                    Success = false,
                    Error = $"Server error: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new KeyValidationResult
            {
                Success = false,
                Error = $"Network error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new KeyValidationResult
            {
                Success = false,
                Error = "Request timed out"
            };
        }
    }
}

public class KeyValidationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public BranchInfo? Branch { get; set; }
}

public class ActivateKeyResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public BranchInfo? Branch { get; set; }
    public string? Message { get; set; }
}

public class BranchInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
