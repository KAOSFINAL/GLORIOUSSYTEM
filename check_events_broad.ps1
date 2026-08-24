$events = Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-15)}
$events | Select-Object -First 15 TimeCreated, Id, ProviderName, Message | Format-List