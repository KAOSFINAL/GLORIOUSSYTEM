$events = Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-30)}
$filtered = $events | Where-Object {
    ($_.ProviderName -like '*.NET*') -or ($_.ProviderName -like '*Application Error*') -or ($_.Message -like '*GLORIOUSSYSTEM*')
}
$filtered | Select-Object -First 10 TimeCreated, ProviderName, Message | Format-List