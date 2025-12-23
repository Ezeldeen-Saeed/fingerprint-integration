namespace ZkFingerBridge.Models;

/// <summary>
/// Response from the biometric logs batch endpoint
/// </summary>
public sealed record BiometricLogsResponse(
    bool Success,
    string Status,
    string Message,
    BiometricLogsSummary? Data,
    ErrorInfo? Errors
);

public sealed record BiometricLogsSummary(
    int TotalReceived,
    int TotalStored,
    int TotalProcessed,
    int TotalDuplicated,
    int TotalMatched,
    int TotalUnmatched,
    int TotalFailed
);

/// <summary>
/// Error information from batch processing
/// </summary>
public sealed record ErrorInfo(
    bool HasErrors,
    UnmatchedEmployeesInfo? UnmatchedEmployees,
    object? ProcessingErrors
);

/// <summary>
/// Information about employees not found in the HR system
/// </summary>
public sealed record UnmatchedEmployeesInfo(
    int Count,
    string Message,
    string ActionRequired,
    List<UnmatchedEmployeeError>? Details
);

/// <summary>
/// Details about a specific unmatched employee
/// </summary>
public sealed record UnmatchedEmployeeError(
    int CompanyId,
    int BranchId,
    string EmployeeId,
    string Error
);
