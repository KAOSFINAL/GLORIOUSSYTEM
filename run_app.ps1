$ErrorActionPreference = "Stop"
try {
    & 'C:\Dev\GLORIOUSSYSTEM\src\GLORIOUSSYSTEM.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\GLORIOUSSYSTEM.App.exe' 2>&1
} catch {
    Write-Host "EXCEPTION: $($_.Exception.ToString())"
}
Write-Host "Done"