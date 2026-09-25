@echo off
REM Scaffold DbContext da SQL Server

dotnet ef dbcontext scaffold "Server=localhost\SQLEXPRESS;Database=K-Flow;User ID=ArmoniaSW;Password=ArmoniaSW$2023;MultipleActiveResultSets=True;Encrypt=False" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --context dbContext --force

pause
