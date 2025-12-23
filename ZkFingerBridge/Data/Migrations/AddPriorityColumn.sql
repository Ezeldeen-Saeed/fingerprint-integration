-- Migration script to add Priority column to existing QueuedLogs table
-- This script can be run on existing queue.db databases to add the new Priority column

-- Check if Priority column already exists before adding
PRAGMA foreign_keys=off;

BEGIN TRANSACTION;

-- Add Priority column if it doesn't exist (default 0 = normal priority)
ALTER TABLE QueuedLogs ADD COLUMN Priority INTEGER NOT NULL DEFAULT 0;

-- Create index for priority-based queue ordering
CREATE INDEX IF NOT EXISTS IX_QueuedLogs_Status_Priority_QueuedAt 
    ON QueuedLogs (Status, Priority, QueuedAt);

COMMIT;

PRAGMA foreign_keys=on;

-- Verify the migration
SELECT COUNT(*) as TotalLogs, 
       AVG(Priority) as AvgPriority 
FROM QueuedLogs;
