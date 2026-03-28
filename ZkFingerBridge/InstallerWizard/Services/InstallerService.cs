using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace InstallerWizard.Services;

/// <summary>
/// Handles the actual installation process
/// </summary>
public class InstallerService
{
    private const string ServiceName = "ZkFingerBridge";
    private const string ServiceDisplayName = "ZkFingerBridge Attendance Sync";
    private const string ServiceDescription = "Syncs fingerprint attendance logs from ZK devices to Firstsoft HR system";

    public async Task<bool> InstallAsync(InstallConfig config, IProgress<InstallProgress> progress)
    {
        try
        {
            // Step 1: Stop existing service if running (10%)
            progress.Report(new InstallProgress(5, "Checking for existing installation..."));
            await Task.Delay(500);
            
            if (ServiceExists())
            {
                progress.Report(new InstallProgress(8, "Stopping existing service..."));
                await StopServiceAsync();
                
                progress.Report(new InstallProgress(12, "Removing existing service..."));
                await DeleteServiceAsync();
            }

            // Step 2: Create installation directory (20%)
            progress.Report(new InstallProgress(20, $"Creating directory: {config.InstallPath}"));
            Directory.CreateDirectory(config.InstallPath);
            await Task.Delay(300);

            // Step 3: Extract/copy application files (50%)
            progress.Report(new InstallProgress(25, "Extracting application files..."));
            await ExtractFilesAsync(config.InstallPath, progress);

            // Step 4: Register zkemkeeper.dll (60%)
            progress.Report(new InstallProgress(60, "Registering ZK SDK..."));
            await RegisterSdkAsync(config.InstallPath);

            // Step 5: Configure appsettings.json (70%)
            progress.Report(new InstallProgress(70, "Writing configuration..."));
            await WriteConfigurationAsync(config);

            // Step 6: Create Windows Service (80%)
            progress.Report(new InstallProgress(80, "Installing Windows Service..."));
            await CreateServiceAsync(config.InstallPath);

            // Step 7: Start service (90%)
            progress.Report(new InstallProgress(90, "Starting service..."));
            await StartServiceAsync();

            // Done
            progress.Report(new InstallProgress(100, "✓ Installation complete!"));
            return true;
        }
        catch (Exception ex)
        {
            progress.Report(new InstallProgress(0, $"✗ Error: {ex.Message}"));
            return false;
        }
    }

    public async Task<bool> InstallSilent(string installationKey)
    {
        // Silent installation for command-line usage
        var config = new InstallConfig
        {
            InstallPath = @"C:\Program Files (x86)\ZkFingerBridge",
            InstallationKey = installationKey,
            BranchId = "1",
            BranchName = "Default",
            DeviceIp = "auto",
            CompanyId = "1"
        };

        var progress = new Progress<InstallProgress>(p => Console.WriteLine($"[{p.Percentage}%] {p.Status}"));
        return await InstallAsync(config, progress);
    }

    #region Service Management

    private bool ServiceExists()
    {
        try
        {
            var result = RunCommand("sc.exe", $"query {ServiceName}");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task StopServiceAsync()
    {
        RunCommand("sc.exe", $"stop {ServiceName}");
        await Task.Delay(3000); // Wait for service to stop
    }

    private async Task DeleteServiceAsync()
    {
        RunCommand("sc.exe", $"delete {ServiceName}");
        await Task.Delay(1000);
    }

    private async Task CreateServiceAsync(string installPath)
    {
        var exePath = Path.Combine(installPath, "ZkFingerBridge.exe");
        
        // Create service
        var createResult = RunCommand("sc.exe", 
            $"create {ServiceName} binPath= \"\\\"{exePath}\\\"\" start= auto DisplayName= \"{ServiceDisplayName}\"");
        
        if (createResult.ExitCode != 0)
        {
            throw new Exception($"Failed to create service: {createResult.Output}");
        }

        // Set description
        RunCommand("sc.exe", $"description {ServiceName} \"{ServiceDescription}\"");

        // Configure failure actions (restart on failure)
        RunCommand("sc.exe", $"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");
        
        await Task.Delay(500);
    }

    private async Task StartServiceAsync()
    {
        var result = RunCommand("sc.exe", $"start {ServiceName}");
        await Task.Delay(2000);
        
        // Verify service is running
        var queryResult = RunCommand("sc.exe", $"query {ServiceName}");
        if (!queryResult.Output.Contains("RUNNING"))
        {
            // Service may need the setup wizard first - that's OK
            Console.WriteLine("Note: Service may require configuration before running.");
        }
    }

    #endregion

    #region File Operations

    private async Task ExtractFilesAsync(string installPath, IProgress<InstallProgress> progress)
    {
        // Check if we have embedded resources or need to copy from source
        // For now, we'll look for a "payload" folder next to the installer
        
        var installerDir = AppContext.BaseDirectory;
        var payloadZip = Path.Combine(installerDir, "payload.zip");
        var payloadDir = Path.Combine(installerDir, "payload");

        if (File.Exists(payloadZip))
        {
            // Extract from ZIP
            progress.Report(new InstallProgress(30, "Extracting from archive..."));
            ZipFile.ExtractToDirectory(payloadZip, installPath, overwriteFiles: true);
        }
        else if (Directory.Exists(payloadDir))
        {
            // Copy from folder
            progress.Report(new InstallProgress(30, "Copying files..."));
            await CopyDirectoryAsync(payloadDir, installPath, progress);
        }
        else
        {
            // Try to find the published build
            var publishPath = FindPublishPath();
            if (!string.IsNullOrEmpty(publishPath) && Directory.Exists(publishPath))
            {
                progress.Report(new InstallProgress(30, "Copying from publish output..."));
                await CopyDirectoryAsync(publishPath, installPath, progress);
            }
            else
            {
                throw new Exception("Could not find application files to install. " +
                    "Please ensure payload.zip or payload folder exists next to the installer.");
            }
        }

        progress.Report(new InstallProgress(55, "Files extracted successfully"));
    }

    private string? FindPublishPath()
    {
        // Try common publish paths
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "ZkFingerBridge", "bin", "Release", "net8.0-windows", "win-x86", "publish"),
            Path.Combine(AppContext.BaseDirectory, "..", "ZkFingerBridge", "publish"),
            @"C:\Files\Work\fingerprint-integration\ZkFingerBridge\bin\Release\net8.0-windows\win-x86\publish"
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private async Task CopyDirectoryAsync(string source, string destination, IProgress<InstallProgress> progress)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var totalFiles = files.Length;
        var copiedFiles = 0;

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(source, file);
            var destPath = Path.Combine(destination, relativePath);
            
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
            
            copiedFiles++;
            var percent = 30 + (int)((double)copiedFiles / totalFiles * 25);
            if (copiedFiles % 10 == 0 || copiedFiles == totalFiles)
            {
                progress.Report(new InstallProgress(percent, $"Copying: {relativePath}"));
            }
        }

        await Task.Delay(100);
    }

    #endregion

    #region SDK Registration

    private async Task RegisterSdkAsync(string installPath)
    {
        var dllPath = Path.Combine(installPath, "zkemkeeper.dll");
        
        if (File.Exists(dllPath))
        {
            // Register as 32-bit COM component
            var regsvr32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "regsvr32.exe");
            var result = RunCommand(regsvr32, $"/s \"{dllPath}\"");
            
            if (result.ExitCode != 0)
            {
                // Try alternative registration
                RunCommand("regsvr32.exe", $"/s \"{dllPath}\"");
            }
        }
        
        await Task.Delay(500);
    }

    #endregion

    #region Configuration

    private async Task WriteConfigurationAsync(InstallConfig config)
    {
        var configPath = Path.Combine(config.InstallPath, "appsettings.json");
        
        var settings = new
        {
            Logging = new
            {
                LogLevel = new
                {
                    Default = "Information",
                    MicrosoftHostingLifetime = "Information"
                }
            },
            ZkDevice = new
            {
                IpAddress = config.DeviceIp,
                Port = 4370,
                MachineNumber = 1,
                CommPassword = (string?)null,
                AutoDiscoverySubnet = "192.168.1",
                EnableAutoDiscovery = config.DeviceIp == "auto",
                BranchId = int.TryParse(config.BranchId, out var bid) ? bid : 1
            },
            HrApi = new
            {
                BaseUrl = "https://api.firstsoft.io",
                AttendanceEndpoint = "/api/biometric/logs",
                CompaniesEndpoint = "/api/biometric/companies",
                ApiKey = (string?)null,
                AuthorizationScheme = "Bearer",
                TimeoutSeconds = 30,
                BatchSize = 100,
                CompanyId = int.TryParse(config.CompanyId, out var cid) ? cid : 1
            },
            Queue = new
            {
                MaxRetryAttempts = 10,
                RetryBackoffSeconds = 60,
                DatabasePath = "queue.db"
            },
            Sync = new
            {
                IntervalSeconds = 10,
                ClearDeviceLogsAfterSync = false,
                StateFilePath = (string?)null
            },
            _configured = true
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(configPath, json);
    }

    #endregion

    #region Helpers

    private (int ExitCode, string Output) RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (-1, "Failed to start process");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            return (process.ExitCode, output + error);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    #endregion
}

public class InstallConfig
{
    public string InstallPath { get; set; } = "";
    public string InstallationKey { get; set; } = "";
    public string BranchId { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string DeviceIp { get; set; } = "";
    public string CompanyId { get; set; } = "";
}

public class InstallProgress
{
    public int Percentage { get; }
    public string Status { get; }

    public InstallProgress(int percentage, string status)
    {
        Percentage = percentage;
        Status = status;
    }
}
