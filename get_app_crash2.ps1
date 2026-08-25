# Capture ALL Application events from the last 5 minutes - look for any crash/error
$events = Get-WinEvent -FilterHashtable @{
    LogName = 'Application'
    StartTime = (Get-Date).AddMinutes(-5)
} | Where-Object {
    $_.Level -le 3 -or $_.Id -eq 1000 -or $_.Id -eq 1001 -or $_.Id -eq 1026 -or $_.ProviderName -like '*.NET*' -or $_.ProviderName -like '*Application Error*' -or $_.ProviderName -like '*MAUI*' -or $_.ProviderName -like '*Windows Error*'
} | Select-Object -First 30 TimeCreated, Id, Level, ProviderName, Message

$events | Format-List | Out-String -Width 4000