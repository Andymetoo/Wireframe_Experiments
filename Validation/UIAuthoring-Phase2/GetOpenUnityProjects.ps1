$ErrorActionPreference = 'Stop'
# Command lines can include Hub authentication tokens. Emit only the project path and role.
Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | ForEach-Object {
    $projectMatch = [regex]::Match($_.CommandLine, '(?i)-projectpath"?\s+"?([^"\r\n]+?)(?:"|\s+-|$)')
    $projectPath = if ($projectMatch.Success) { $projectMatch.Groups[1].Value.Trim() } else { '(unknown)' }
    [pscustomobject]@{ ProcessId = $_.ProcessId; ProjectPath = $projectPath; AssetWorker = ($_.CommandLine -match '(?i)AssetImportWorker') }
} | Format-Table -AutoSize
