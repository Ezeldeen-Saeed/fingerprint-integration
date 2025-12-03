using System.ComponentModel.DataAnnotations;

namespace ZkFingerBridge.Configuration;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    [Range(10, 3600)]
    public int IntervalSeconds { get; set; } = 60;

    public bool ClearDeviceLogsAfterSync { get; set; } = false;

    /// <summary>
    /// Optional path override for the state file that keeps track of the last synced timestamp.
    /// When null, defaults to "sync-state.json" under the content root.
    /// </summary>
    public string? StateFilePath { get; set; }
        = null;
}
