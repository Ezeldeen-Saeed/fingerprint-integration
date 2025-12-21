using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZkFingerBridge.UI;

/// <summary>
/// Helper class to save wizard selections to appsettings.json
/// </summary>
public static class SettingsSaver
{
    /// <summary>
    /// Updates appsettings.json with the selected company, branch, and device IP
    /// </summary>
    public static void SaveSettings(
        string appSettingsPath,
        int companyId,
        string? companyName,
        int branchId,
        string? branchName,
        string? deviceIpAddress = null)
    {
        var json = File.ReadAllText(appSettingsPath);
        var jsonNode = JsonNode.Parse(json) ?? throw new InvalidOperationException("Invalid appsettings.json");

        // Update HrApi section
        var hrApi = jsonNode["HrApi"] ?? throw new InvalidOperationException("HrApi section not found");
        hrApi["CompanyId"] = companyId;
        hrApi["CompanyName"] = companyName;

        // Update ZkDevice section
        var zkDevice = jsonNode["ZkDevice"] ?? throw new InvalidOperationException("ZkDevice section not found");
        zkDevice["BranchId"] = branchId;
        zkDevice["BranchName"] = branchName;
        
        // Save device IP if provided (disable auto-discovery when manual IP is set)
        if (!string.IsNullOrWhiteSpace(deviceIpAddress))
        {
            zkDevice["IpAddress"] = deviceIpAddress;
            zkDevice["EnableAutoDiscovery"] = false;
        }

        // Write back to file with proper formatting
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        File.WriteAllText(appSettingsPath, jsonNode.ToJsonString(options));
    }

    /// <summary>
    /// Checks if the wizard has been completed (company and branch are configured)
    /// </summary>
    public static bool IsConfigured(string appSettingsPath)
    {
        try
        {
            var json = File.ReadAllText(appSettingsPath);
            var jsonNode = JsonNode.Parse(json);
            
            var companyId = jsonNode?["HrApi"]?["CompanyId"]?.GetValue<int>() ?? 0;
            var branchId = jsonNode?["ZkDevice"]?["BranchId"]?.GetValue<int>() ?? 0;
            
            return companyId > 0 && branchId > 0;
        }
        catch
        {
            return false;
        }
    }
}
