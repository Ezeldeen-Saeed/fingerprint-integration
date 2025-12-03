# ZK Finger Bridge

A .NET Worker Service that periodically pulls attendance logs from a ZKTeco (ZKFinger) terminal via the COM `zkemkeeper` SDK and forwards them to an HR system HTTP API.

## Prerequisites

1. Install the ZKTeco communication SDK and register `zkemkeeper.dll` (see `Standalone-SDK/Communication Protocol SDK(32Bit Ver6.2.4.11)` inside the repo). Run the provided `Auto-install_sdk.bat` as administrator on the target machine.
2. Ensure .NET 8/10 SDK is available on the host.
3. Device must be reachable over TCP/IP or USB.

## Configuration (`appsettings.json` or environment variables)

```json
"ZkDevice": {
  "IpAddress": "192.168.1.201",
  "Port": 4370,
  "MachineNumber": 1,
  "CommPassword": null
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
