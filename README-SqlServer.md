# PrintHero SQL Server Setup Guide

PrintHero now supports SQL Server for license management. This guide explains how to set up and configure SQL Server for PrintHero.

## Prerequisites

- SQL Server (Local instance, SQL Server Express, or Azure SQL Database)
- SQL Server Management Studio (SSMS) or Azure Data Studio (optional)

## Quick Setup

### Option 1: Automatic Setup (Recommended)
PrintHero will automatically create the database and tables when it first runs.

1. **Set Connection String** (choose one method):
   
   **Method A: Environment Variable**
   ```cmd
   set PRINTHERO_DB_CONNECTION="Server=localhost;Database=PrintHero;Integrated Security=true;TrustServerCertificate=true;"
   ```
   
   **Method B: Edit appsettings.json**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=PrintHero;Integrated Security=true;TrustServerCertificate=true;"
     }
   }
   ```

2. **Run PrintHero** - Database and tables will be created automatically

### Option 2: Manual Setup
1. **Run the SQL Script**:
   - Open SQL Server Management Studio
   - Connect to your SQL Server instance
   - Open and execute `SqlServer-Setup.sql`

2. **Configure Connection String** (same as Option 1)

## Connection String Examples

### Local SQL Server (Windows Authentication)
```
Server=localhost;Database=PrintHero;Integrated Security=true;TrustServerCertificate=true;
```

### Local SQL Server Express
```
Server=localhost\SQLEXPRESS;Database=PrintHero;Integrated Security=true;TrustServerCertificate=true;
```

### SQL Server with Username/Password
```
Server=localhost;Database=PrintHero;User ID=sa;Password=yourpassword;TrustServerCertificate=true;
```

### Azure SQL Database
```
Server=tcp:your-server.database.windows.net,1433;Initial Catalog=PrintHero;Persist Security Info=False;User ID=your-username;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

## Database Structure

### Tables Created:
- **LicenseKeys** - Stores valid license keys
- **LicenseActivations** - Tracks license activations per machine
- **MonitoredFolders** - PrintHero folder monitoring settings
- **PrintJobs** - Print job history
- **Settings** - Application settings

### Sample License Keys (Created Automatically):
- `DEMO2025PRINTHERO` - Demo (90 days)
- `TRIAL2025PRINT01` - Trial (30 days)
- `FULL2025PRINT001` - Full (1 year, 2 activations)
- `PREM2025PRINT001` - Premium (no expiry, 5 activations)
- `TEST2025PRINT001` - Test (7 days)

## Adding New License Keys

### Method 1: SQL Query
```sql
INSERT INTO LicenseKeys (LicenseKey, CustomerName, CustomerEmail, LicenseType, IsActive, CreatedDate, ExpiryDate, MaxActivations, Notes)
VALUES ('YOUR-LICENSE-KEY', 'Customer Name', 'email@company.com', 'Standard', 1, GETDATE(), DATEADD(year, 1, GETDATE()), 1, 'Customer license');
```

### Method 2: Use Management Script
1. Edit `AddLicenseKey.sql` with your license details
2. Execute the script in SSMS

## Viewing License Status

### Check All Active Licenses:
```sql
SELECT 
    lk.LicenseKey,
    lk.CustomerName,
    lk.LicenseType,
    lk.ExpiryDate,
    lk.MaxActivations,
    COUNT(la.Id) as CurrentActivations
FROM LicenseKeys lk
LEFT JOIN LicenseActivations la ON lk.LicenseKey = la.LicenseKey AND la.IsActive = 1
WHERE lk.IsActive = 1
GROUP BY lk.LicenseKey, lk.CustomerName, lk.LicenseType, lk.ExpiryDate, lk.MaxActivations
ORDER BY lk.CreatedDate DESC;
```

### Check Activations for Specific License:
```sql
SELECT * FROM LicenseActivations 
WHERE LicenseKey = 'YOUR-LICENSE-KEY' AND IsActive = 1;
```

## Troubleshooting

### Common Issues:

1. **Connection Failed**:
   - Check SQL Server is running
   - Verify connection string
   - Check firewall settings
   - Ensure database exists

2. **Permission Errors**:
   - Ensure user has CREATE DATABASE permissions
   - Grant SELECT, INSERT, UPDATE permissions on PrintHero database

3. **Azure SQL Database**:
   - Ensure firewall rules allow your IP
   - Use correct Azure SQL connection string format
   - Enable "Allow Azure services" if needed

### Enable SQL Server Browser (for named instances):
```cmd
net start SQLBrowser
```

### Enable TCP/IP Protocol:
1. Open SQL Server Configuration Manager
2. Expand SQL Server Network Configuration
3. Click on Protocols for [INSTANCE_NAME]
4. Right-click TCP/IP and select Enable
5. Restart SQL Server service

## Environment Variables

Set these environment variables for configuration:

```cmd
# Database connection
set PRINTHERO_DB_CONNECTION="Server=localhost;Database=PrintHero;Integrated Security=true;TrustServerCertificate=true;"

# Optional: Disable automatic database creation
set PRINTHERO_AUTO_CREATE_DB=false
```

## Migration from SQLite

If you were previously using SQLite, PrintHero will automatically use SQL Server once configured. The old SQLite database will remain but won't be used.

## Support

For additional support:
1. Check SQL Server error logs
2. Check PrintHero application logs in `%APPDATA%\PrintHero\logs\`
3. Verify database permissions and connectivity