using System.ComponentModel.DataAnnotations;

namespace ZkFingerBridge.Configuration;

public sealed class HrApiOptions
{
    public const string SectionName = "HrApi";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://hr.example.com";

    [Required]
    public string AttendanceEndpoint { get; set; } = "/api/attendance";

    /// <summary>
    /// Bearer token or API key used for authorization header.
    /// </summary>
    public string? ApiKey { get; set; }
        = null;

    /// <summary>
    /// Authorization scheme (e.g. "Bearer" or "Basic"). Defaults to Bearer when ApiKey is provided.
    /// </summary>
    public string AuthorizationScheme { get; set; } = "Bearer";

    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 100;
}
