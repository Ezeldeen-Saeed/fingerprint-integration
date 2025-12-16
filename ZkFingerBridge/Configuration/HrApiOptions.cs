using System.ComponentModel.DataAnnotations;

namespace ZkFingerBridge.Configuration;

public sealed class HrApiOptions
{
    public const string SectionName = "HrApi";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.firstsoft.io";

    [Required]
    public string AttendanceEndpoint { get; set; } = "/api/biometric/punch";

    /// <summary>
    /// Company ID required by the Firstsoft.io API.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int CompanyId { get; set; }

    /// <summary>
    /// Company name for display purposes (set by wizard)
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Endpoint to fetch companies and branches list
    /// </summary>
    public string CompaniesEndpoint { get; set; } = "/api/biometric/companies";

    /// <summary>
    /// Bearer token or API key used for authorization header.
    /// </summary>
    public string? ApiKey { get; set; } = null;

    /// <summary>
    /// Authorization scheme (e.g. "Bearer" or "Basic"). Defaults to Bearer when ApiKey is provided.
    /// </summary>
    public string AuthorizationScheme { get; set; } = "Bearer";

    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 100;
}
