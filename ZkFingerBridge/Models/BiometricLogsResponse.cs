namespace ZkFingerBridge.Models;

/// <summary>
/// Response from the biometric logs batch endpoint
/// </summary>
public sealed record BiometricLogsResponse(
    bool Success,
    string Message,
    BiometricLogsData? Data
);

public sealed record BiometricLogsData(
    int TotalReceived,
    int TotalStored,
    int TotalMatched,
    int TotalUnmatched
);
