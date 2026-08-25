cd "C:\Dev\GLORIOUSSYSTEM\src\GLORIOUSSYSTEM.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64"
$proc = Start-Process -FilePath '.\GLORIOUSSYSTEM.App.exe' -PassThru -RedirectStandardOutput stdout.txt -RedirectStandardError stderr.txt
Start-Sleep -Seconds 5
Get-Content stdout.txt -ErrorAction SilentlyContinue
Get-Content stderr.txt -ErrorAction SilentlyContinue
Write-Host "Process: $($proc.Id) Exited: $($proc.HasExited) ExitCode: $($proc.ExitCode)"