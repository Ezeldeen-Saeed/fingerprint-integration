using System.Text.Json.Serialization;

namespace ZkFingerBridge.Models;

/// <summary>
/// Response from the companies/branches API endpoint
/// </summary>
public record CompaniesResponse(
    bool Success,
    List<Company> Data);

public record Company(
    [property: JsonPropertyName("company_id")] int CompanyId,
    [property: JsonPropertyName("company_name")] string CompanyName,
    List<Branch> Branches);

public record Branch(
    [property: JsonPropertyName("branch_id")] int BranchId,
    [property: JsonPropertyName("branch_name")] string BranchName,
    string? Coordinates);
