-- PrintHero SQL Server Database Setup Script
-- Run this script on your SQL Server instance to create the database and tables

-- Create the PrintHero database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PrintHero')
BEGIN
    CREATE DATABASE PrintHero;
    PRINT 'PrintHero database created successfully.';
END
ELSE
BEGIN
    PRINT 'PrintHero database already exists.';
END

-- Use the PrintHero database
USE PrintHero;

-- Create LicenseKeys table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LicenseKeys' AND xtype='U')
BEGIN
    CREATE TABLE LicenseKeys (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        LicenseKey NVARCHAR(50) NOT NULL UNIQUE,
        CustomerName NVARCHAR(100),
        CustomerEmail NVARCHAR(100),
        ProductName NVARCHAR(50) DEFAULT 'PrintHero',
        LicenseType NVARCHAR(20) DEFAULT 'Standard',
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        ExpiryDate DATETIME,
        MaxActivations INT DEFAULT 1,
        Notes NVARCHAR(500)
    );
    PRINT 'LicenseKeys table created.';
END

-- Create LicenseActivations table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LicenseActivations' AND xtype='U')
BEGIN
    CREATE TABLE LicenseActivations (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        LicenseKey NVARCHAR(50) NOT NULL,
        MachineId NVARCHAR(50) NOT NULL,
        MachineName NVARCHAR(100),
        UserName NVARCHAR(100),
        ActivationDate DATETIME NOT NULL DEFAULT GETDATE(),
        LastAccessed DATETIME,
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_LicenseActivations_LicenseKeys 
            FOREIGN KEY (LicenseKey) REFERENCES LicenseKeys(LicenseKey)
    );
    PRINT 'LicenseActivations table created.';
END


-- Create PrintJobs table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PrintJobs' AND xtype='U')
BEGIN
    CREATE TABLE PrintJobs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FilePath NVARCHAR(500) NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        PrinterName NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        PrintedAt DATETIME,
        Status INT NOT NULL DEFAULT 0,
        ErrorMessage NVARCHAR(1000),
        LicenseKey NVARCHAR(50),
        MachineId NVARCHAR(50),
        FileSizeBytes BIGINT NOT NULL DEFAULT 0,
        PostPrintAction INT NOT NULL DEFAULT 0,
        MovedToPath NVARCHAR(500),
        CONSTRAINT FK_PrintJobs_LicenseKeys 
            FOREIGN KEY (LicenseKey) REFERENCES LicenseKeys(LicenseKey)
    );
    PRINT 'PrintJobs table created.';
END


-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LicenseKeys_LicenseKey')
    CREATE INDEX IX_LicenseKeys_LicenseKey ON LicenseKeys(LicenseKey);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LicenseKeys_IsActive')
    CREATE INDEX IX_LicenseKeys_IsActive ON LicenseKeys(IsActive);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LicenseActivations_LicenseKey')
    CREATE INDEX IX_LicenseActivations_LicenseKey ON LicenseActivations(LicenseKey);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LicenseActivations_MachineId')
    CREATE INDEX IX_LicenseActivations_MachineId ON LicenseActivations(MachineId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LicenseActivations_IsActive')
    CREATE INDEX IX_LicenseActivations_IsActive ON LicenseActivations(IsActive);

PRINT 'Indexes created.';

-- Insert sample license keys for testing (only if table is empty)
IF (SELECT COUNT(*) FROM LicenseKeys) = 0
BEGIN
    INSERT INTO LicenseKeys (LicenseKey, CustomerName, CustomerEmail, LicenseType, IsActive, CreatedDate, ExpiryDate, MaxActivations, Notes)
    VALUES 
        ('DEMO2025PRINTHERO', 'Demo User', 'demo@printhero.com', 'Demo', 1, GETDATE(), DATEADD(day, 90, GETDATE()), 1, 'Demo license - 90 days'),
        ('TRIAL2025PRINT01', 'Trial User', 'trial@printhero.com', 'Trial', 1, GETDATE(), DATEADD(day, 30, GETDATE()), 1, 'Trial license - 30 days'),
        ('FULL2025PRINT001', 'John Smith', 'john.smith@company.com', 'Full', 1, GETDATE(), DATEADD(year, 1, GETDATE()), 2, 'Full license - 1 year, 2 activations'),
        ('PREM2025PRINT001', 'Jane Doe', 'jane.doe@business.com', 'Premium', 1, GETDATE(), NULL, 5, 'Premium license - no expiry, 5 activations'),
        ('TEST2025PRINT001', 'Test Account', 'test@printhero.com', 'Test', 1, GETDATE(), DATEADD(day, 7, GETDATE()), 1, 'Test license - 7 days for testing');
    
    PRINT 'Sample license keys inserted.';
END
ELSE
BEGIN
    PRINT 'License keys already exist, skipping sample data.';
END

-- Display current license keys
SELECT 
    LicenseKey,
    CustomerName,
    LicenseType,
    CreatedDate,
    ExpiryDate,
    MaxActivations,
    (SELECT COUNT(*) FROM LicenseActivations WHERE LicenseActivations.LicenseKey = LicenseKeys.LicenseKey AND IsActive = 1) as CurrentActivations,
    Notes
FROM LicenseKeys 
WHERE IsActive = 1 
ORDER BY CreatedDate DESC;

PRINT 'PrintHero database setup completed successfully!';