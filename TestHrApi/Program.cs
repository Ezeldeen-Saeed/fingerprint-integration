using Microsoft.EntityFrameworkCore;
using TestHrApi.Data;
using TestHrApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add DbContext
builder.Services.AddDbContext<FingerprintDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FingerprintDb")));

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FingerprintDbContext>();
    db.Database.EnsureCreated();
    app.Logger.LogInformation("✓ Database ready: {Database}", db.Database.GetDbConnection().Database);
}

// Test endpoint to verify API is running
app.MapGet("/", async (FingerprintDbContext db) => new
{
    Status = "OK",
    Message = "Test HR API is running with SQL LocalDB",
    Database = db.Database.GetDbConnection().Database,
    Endpoint = "/api/attendance",
    TotalLogsInDatabase = await db.AttendanceLogs.CountAsync()
});

// Main endpoint to receive attendance logs
app.MapPost("/api/attendance", async (List<AttendanceLogDto> logs, FingerprintDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("========================================");
    logger.LogInformation("📥 Received {Count} attendance log(s)", logs.Count);
    logger.LogInformation("========================================");

    var entities = new List<AttendanceLog>();

    foreach (var log in logs)
    {
        logger.LogInformation("Employee: {EmployeeId} | Time: {PunchTime} | Type: {PunchType} | Verify: {VerifyMode}",
            log.EmployeeId,
            log.PunchTime,
            log.PunchType,
            log.VerifyMode);

        var entity = new AttendanceLog
        {
            EmployeeId = log.EmployeeId,
            PunchTime = log.PunchTime,
            VerifyMode = log.VerifyMode,
            PunchType = log.PunchType,
            WorkCode = log.WorkCode
        };

        entities.Add(entity);
    }

    // Save to database
    await db.AttendanceLogs.AddRangeAsync(entities);
    await db.SaveChangesAsync();

    var totalInDb = await db.AttendanceLogs.CountAsync();

    logger.LogInformation("----------------------------------------");
    logger.LogInformation("✅ Saved to database. Total logs: {Total}", totalInDb);
    logger.LogInformation("========================================");

    return Results.Ok(new
    {
        Success = true,
        Message = $"Successfully saved {logs.Count} log(s) to database",
        TotalInDatabase = totalInDb
    });
});

// Endpoint to view all received logs
app.MapGet("/api/attendance", async (FingerprintDbContext db, int? limit, string? employeeId) =>
{
    var query = db.AttendanceLogs.AsQueryable();

    // Filter by employeeId if provided
    if (!string.IsNullOrEmpty(employeeId))
    {
        query = query.Where(l => l.EmployeeId == employeeId);
    }

    // Order by most recent first
    query = query.OrderByDescending(l => l.PunchTime);

    // Limit results if specified
    if (limit.HasValue && limit.Value > 0)
    {
        query = query.Take(limit.Value);
    }

    var logs = await query.ToListAsync();

    return Results.Ok(new
    {
        Success = true,
        TotalLogs = await db.AttendanceLogs.CountAsync(),
        FilteredCount = logs.Count,
        Logs = logs
    });
});

// Endpoint to get statistics
app.MapGet("/api/attendance/stats", async (FingerprintDbContext db) =>
{
    var totalLogs = await db.AttendanceLogs.CountAsync();
    var uniqueEmployees = await db.AttendanceLogs
        .Select(l => l.EmployeeId)
        .Distinct()
        .CountAsync();
    
    var lastLog = await db.AttendanceLogs
        .OrderByDescending(l => l.PunchTime)
        .FirstOrDefaultAsync();

    return Results.Ok(new
    {
        Success = true,
        TotalLogs = totalLogs,
        UniqueEmployees = uniqueEmployees,
        LastLog = lastLog,
        DatabaseName = db.Database.GetDbConnection().Database
    });
});

// Endpoint to clear logs (for testing)
app.MapDelete("/api/attendance", async (FingerprintDbContext db, ILogger<Program> logger) =>
{
    var count = await db.AttendanceLogs.CountAsync();
    
    db.AttendanceLogs.RemoveRange(db.AttendanceLogs);
    await db.SaveChangesAsync();
    
    logger.LogInformation("🗑️ Cleared {Count} log(s) from database", count);
    
    return Results.Ok(new
    {
        Success = true,
        Message = $"Cleared {count} log(s) from database"
    });
});

// ========================================
// Installation Key Management Endpoints
// ========================================

// Generate a new installation key for a branch
app.MapPost("/api/keys/generate", async (string branchId, string branchName, FingerprintDbContext db, ILogger<Program> logger, HttpContext context) =>
{
    // Generate unique key: BRANCH-YEAR-RANDOM
    var random = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
    var key = $"{branchId.ToUpper()}-{DateTime.Now.Year}-{random}";
    
    var installKey = new InstallationKey
    {
        Key = key,
        BranchId = branchId,
        BranchName = branchName,
        Used = false,
        CreatedBy = "Admin",
        CreatedAt = DateTime.UtcNow
    };
    
    db.InstallationKeys.Add(installKey);
    await db.SaveChangesAsync();
    
    logger.LogInformation("🔑 Generated installation key: {Key} for branch: {BranchName}", key, branchName);
    
    return Results.Ok(new
    {
        Success = true,
        Key = key,
        BranchId = branchId,
        BranchName = branchName,
        Message = $"Installation key generated successfully"
    });
});

// Activate/validate an installation key
app.MapPost("/api/installer/activate", async (ActivateKeyRequest request, FingerprintDbContext db, ILogger<Program> logger, HttpContext context) =>
{
    var installKey = await db.InstallationKeys
        .FirstOrDefaultAsync(k => k.Key == request.InstallationKey);
    
    if (installKey == null)
    {
        logger.LogWarning("❌ Invalid installation key attempted: {Key}", request.InstallationKey);
        return Results.Json(new ActivateKeyResponse
        {
            Success = false,
            Error = "Invalid installation key"
        }, statusCode: 404);
    }
    
    if (installKey.Used)
    {
        logger.LogWarning("❌ Already used installation key attempted: {Key}", request.InstallationKey);
        return Results.Json(new ActivateKeyResponse
        {
            Success = false,
            Error = "This installation key has already been used",
            UsedAt = installKey.UsedAt,
            UsedBy = installKey.UsedByIp
        }, statusCode: 403);
    }
    
    if (installKey.ExpiresAt.HasValue && DateTime.UtcNow > installKey.ExpiresAt.Value)
    {
        logger.LogWarning("❌ Expired installation key attempted: {Key}", request.InstallationKey);
        return Results.Json(new ActivateKeyResponse
        {
            Success = false,
            Error = "This installation key has expired"
        }, statusCode: 403);
    }
    
    // Mark as used
    installKey.Used = true;
    installKey.UsedAt = DateTime.UtcNow;
    installKey.UsedByIp = context.Connection.RemoteIpAddress?.ToString();
    
    await db.SaveChangesAsync();
    
    logger.LogInformation("✅ Installation key activated: {Key} for branch: {BranchName} from IP: {IP}", 
        installKey.Key, installKey.BranchName, installKey.UsedByIp);
    
    return Results.Ok(new ActivateKeyResponse
    {
        Success = true,
        Branch = new Branch
        {
            Id = installKey.BranchId,
            Name = installKey.BranchName
        },
        Message = "Installation key validated successfully"
    });
});

// List all installation keys (for admin)
app.MapGet("/api/keys", async (FingerprintDbContext db) =>
{
    var keys = await db.InstallationKeys
        .OrderByDescending(k => k.CreatedAt)
        .Select(k => new
        {
            k.Key,
            k.BranchId,
            k.BranchName,
            k.Used,
            k.UsedAt,
            k.UsedByIp,
            k.ExpiresAt,
            k.CreatedAt
        })
        .ToListAsync();
    
    return Results.Ok(new
    {
        Success = true,
        Keys = keys,
        TotalKeys = keys.Count,
        UsedKeys = keys.Count(k => k.Used),
        UnusedKeys = keys.Count(k => !k.Used)
    });
});

app.Run("http://localhost:5000");

// DTO to match ZkFingerBridge's AttendanceLog model
public record AttendanceLogDto(
    string EmployeeId,
    DateTimeOffset PunchTime,
    int VerifyMode,
    int PunchType,
    int WorkCode
);
