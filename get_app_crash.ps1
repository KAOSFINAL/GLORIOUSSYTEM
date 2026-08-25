# Capture Application Error events (Event ID 1000) and .NET Runtime errors
$events = Get-WinEvent -FilterHashtable @{
    LogName = 'Application'
    StartTime = (Get-Date).AddMinutes(-5)
    Id = 1000, 1001, 1002, 1026, 1023, 1041
} | Select-Object -First 10 TimeCreated, Id, ProviderName, Message

$events | Format-List | Out-String -Width 4000