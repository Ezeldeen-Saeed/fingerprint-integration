# Test HR API

A minimal .NET API for testing the ZkFingerBridge application with **SQL Server LocalDB** persistent storage.

## Features

- ✅ Receives attendance logs from ZkFingerBridge
- ✅ **Stores logs in SQL Server LocalDB** (persistent storage)
- ✅ Displays logs in console with formatting
- ✅ Provides endpoints to view, filter, and analyze logs
- ✅ Database statistics and reporting

## Database

- **Server**: `(localdb)\MSSQLLocalDB`
- **Database**: `fingerprints`
- **Table**: `AttendanceLogs`

Database is created automatically on first run!

## Endpoints

### `GET /`
Health check - verify API is running and database status

**Response:**
```json
{
  "Status": "OK",
  "Message": "Test HR API is running with SQL LocalDB",
  "Database": "fingerprints",
  "Endpoint": "/api/attendance",
  "TotalLogsInDatabase": 25
}
```

### `POST /api/attendance`
Receive and save attendance logs to database

**Request Body:**
```json
[
  {
    "EmployeeId": "123",
    "PunchTime": "2025-12-04T08:30:00+02:00",
    "VerifyMode": 1,
    "PunchType": 0,
    "WorkCode": 0
  }
]
```

**Response:**
```json
{
  "Success": true,
  "Message": "Successfully saved 1 log(s) to database",
  "TotalInDatabase": 26
}
```

### `GET /api/attendance`
View logs with optional filtering and pagination

**Query Parameters:**
- `limit` - Limit number of results
- `employeeId` - Filter by specific employee

**Examples:**
```powershell
# All logs
curl http://localhost:5000/api/attendance

# Last 10 logs
curl "http://localhost:5000/api/attendance?limit=10"

# Logs for employee 123
curl "http://localhost:5000/api/attendance?employeeId=123"
```

**Response:**
```json
{
  "Success": true,
  "TotalLogs": 150,
  "FilteredCount": 10,
  "Logs": [...]
}
```

### `GET /api/attendance/stats`
Get database statistics ⭐ NEW!

**Response:**
```json
{
  "Success": true,
  "TotalLogs": 150,
  "UniqueEmployees": 12,
  "LastLog": {...},
  "DatabaseName": "fingerprints"
}
```

### `DELETE /api/attendance`
Clear all stored logs from database

**Response:**
```json
{
  "Success": true,
  "Message": "Cleared 25 log(s) from database"
}
```

## Running

```powershell
dotnet run
```

API will be available at: `http://localhost:5000`

On first run, database wil be created automatically:
```
info: ✓ Database ready: fingerprints
info: Now listening on: http://localhost:5000
```

## Testing

```powershell
# Check if running
curl http://localhost:5000

# View statistics
curl http://localhost:5000/api/attendance/stats

# View last 10 logs
curl "http://localhost:5000/api/attendance?limit=10"

# Clear database
curl -X DELETE http://localhost:5000/api/attendance
```

## View Database in Visual Studio

1. **View → SQL Server Object Explorer**
2. Expand: **SQL Server → (localdb)\MSSQLLocalDB → Databases → fingerprints**
3. Expand: **Tables → dbo.AttendanceLogs**
4. Right-click → **View Data**

## Console Output

When ZkFingerBridge sends data:

```
========================================
📥 Received 3 attendance log(s)
========================================
info: Employee: 123 | Time: 2025-12-04T08:30:00+02:00 | Type: 0 | Verify: 1
info: Employee: 456 | Time: 2025-12-04T09:15:00+02:00 | Type: 1 | Verify: 1
info: Employee: 789 | Time: 2025-12-04T10:20:00+02:00 | Type: 0 | Verify: 1
----------------------------------------
✅ Saved to database. Total logs: 28
========================================
```

## Notes

- Data is **persistent** - survives app restarts
- Uses **SQL Server LocalDB** (included with Visual Studio)
- For production, update connection string to use full SQL Server
- Database indexes on `EmployeeId` and `PunchTime` for fast queries

## Next Steps

See [`localdb_guide.md`](../../.gemini/antigravity/brain/.../localdb_guide.md) for:
- Advanced queries
- Database schema details
- Migration to production SQL Server

