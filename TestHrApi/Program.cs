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

app.Run("http://localhost:5000");

// DTO to match ZkFingerBridge's AttendanceLog model
public record AttendanceLogDto(
    string EmployeeId,
    DateTimeOffset PunchTime,
    int VerifyMode,
    int PunchType,
    int WorkCode
);
