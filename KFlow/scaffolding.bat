@echo off
cd /d "%~dp0"

dotnet ef dbcontext scaffold "Server=localhost\SQLEXPRESS;Database=K-Flow;User ID=ArmoniaSW;Password=ArmoniaSW$2023;MultipleActiveResultSets=True;Encrypt=False" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --context dbContext --namespace KFlow.Models --context-namespace KFlow.Models --no-onconfiguring --force

pause
