# Capture recent Application log events for crash diagnosis
$events = Get-WinEvent -FilterHashtable @{
    LogName = 'Application'
    StartTime = (Get-Date).AddMinutes(-10)
} | Select-Object -First 20 TimeCreated, Id, ProviderName, Message

$events | Format-List | Out-String -Width 4000