# Capture recent System log events for crash diagnosis
$events = Get-WinEvent -FilterHashtable @{
    LogName = 'System'
    StartTime = (Get-Date).AddMinutes(-5)
} | Select-Object -First 20 TimeCreated, Id, ProviderName, Message

$events | Format-List | Out-String -Width 4000