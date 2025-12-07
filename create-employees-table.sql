-- Create Employees table manually
-- Run this in SQL Server Management Studio or via sqlcmd

USE FingerprintDb;
GO

-- Create Employees table
CREATE TABLE [Employees] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Enabled] bit NOT NULL,
    [Privilege] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
);
GO

-- Create unique index on EmployeeId
CREATE UNIQUE INDEX [IX_Employees_EmployeeId] ON [Employees] ([EmployeeId]);
GO

-- Verify table was created
SELECT * FROM Employees;
GO

PRINT 'Employees table created successfully!';
GO
