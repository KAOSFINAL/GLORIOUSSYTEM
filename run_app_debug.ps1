cd 'C:\Dev\GLORIOUSSYSTEM\src\GLORIOUSSYSTEM.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64'
$proc = Start-Process -FilePath '.\GLORIOUSSYSTEM.App.exe' -PassThru
Start-Sleep -Seconds 5
Write-Host "Process: $($proc.Id) Exited: $($proc.HasExited) ExitCode: $($proc.ExitCode)"
Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime, CPU, Responding