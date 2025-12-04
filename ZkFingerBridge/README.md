# ZK Finger Bridge

A .NET Worker Service that periodically pulls attendance logs from a ZKTeco (ZKFinger) terminal via the COM `zkemkeeper` SDK and forwards them to an HR system HTTP API.

## Prerequisites

1. Install the ZKTeco communication SDK and register `zkemkeeper.dll` (see `Standalone-SDK/Communication Protocol SDK(32Bit Ver6.2.4.11)` inside the repo). Run the provided `Auto-install_sdk.bat` as administrator on the target machine.
2. Ensure .NET 8/10 SDK is available on the host.
3. Device must be reachable over TCP/IP or USB.

## Configuration (`appsettings.json` or environment variables)

```json
"ZkDevice": {
  "IpAddress": "auto",           // Set to "auto" for auto-discovery, or specific IP like "192.168.0.201"
  "Port": 4370,
  "MachineNumber": 1,
  "CommPassword": null,
  "AutoDiscoverySubnet": "192.168.0",  // Subnet to scan when auto-discovery is enabled
  "EnableAutoDiscovery": true          // Enable auto-discovery on startup
},
"HrApi": {
  "BaseUrl": "https://hr.example.com",
  "AttendanceEndpoint": "/api/attendance",
  "ApiKey": "<token>",
  "AuthorizationScheme": "Bearer",
  "TimeoutSeconds": 30,
  "BatchSize": 100
},
"Sync": {
  "IntervalSeconds": 60,
  "ClearDeviceLogsAfterSync": false,
  "StateFilePath": null
}
```

### Auto-Discovery Feature

If your device has a **dynamic IP address (DHCP)**, you can enable auto-discovery:

1. Set `"IpAddress": "auto"` in configuration
2. Set `"AutoDiscoverySubnet"` to your network subnet (e.g., `"192.168.0"`)
3. Set `"EnableAutoDiscovery": true`

The application will scan the subnet on startup and automatically find the device on port 4370.

**Note**: For production use, it's recommended to set a static IP on the device or use DHCP reservation in your router.


Use `dotnet user-secrets` or environment variables for secrets:

```powershell
dotnet user-secrets set "HrApi:ApiKey" "<token>"
```

## Running locally

```powershell
cd ZkFingerBridge
dotnet run
```

The worker will:
1. Read all logs from the ZKTeco terminal (`ZkDeviceClient`).
2. Filter logs newer than the last synced timestamp stored in `sync-state.json`.
3. Post batches to the HR API (`HrApiClient`).
4. Persist the latest timestamp and optionally clear the device logs.

## Deployment

- Publish self-contained for the target OS: `dotnet publish -c Release -r win-x64 --self-contained true`.
- Run as a Windows Service using `sc create` or `New-Service`, or containerize if access to COM components is not required (device access usually requires Windows Service).
- Ensure the service account has permission to access the COM component and network.

## Troubleshooting

- If you see `zkemkeeper.ZKEM COM component is not registered`, re-run the SDK registration script as administrator.
- Logs are written via `ILogger` (default console + EventLog when running as service). Increase verbosity with `Logging:LogLevel` settings.
- Set `Sync:ClearDeviceLogsAfterSync` to true if the HR system becomes the source of truth; otherwise leave logs on the device.
