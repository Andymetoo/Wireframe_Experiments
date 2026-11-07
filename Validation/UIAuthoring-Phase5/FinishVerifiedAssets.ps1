$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage = Join-Path $PSScriptRoot 'AcceptanceProject'
if (-not (Test-Path (Join-Path $stage 'UIAuthoringPhase5ValidationProject.txt'))) { throw 'Missing isolated-project marker.' }
foreach ($process in Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
    $match = [regex]::Match($process.CommandLine, '(?i)-projectpath"?\s+"?([^"\r\n]+?)(?:"|\s+-|$)')
    if (-not $match.Success) { throw 'Unidentified Unity process; refuse serialized updates.' }
    $openPath = [IO.Path]::GetFullPath($match.Groups[1].Value.Trim()).TrimEnd('\','/')
    if ($openPath -in @($workspace.TrimEnd('\','/'), $stage.TrimEnd('\','/'))) { throw 'Close working and isolated Unity projects before serialized updates.' }
}
foreach ($project in @($workspace, $stage)) {
    $lockPath = Join-Path $project 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        try { $handle = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $handle.Dispose() }
        catch { throw "Unity project lock is held: $project" }
    }
}
$relativeRoot = 'Assets/Experiments/IceResurfacer'
$scene = Join-Path $workspace ($relativeRoot + '/Scenes/IceResurfacer_AuthoredMachine.unity')
if ((Get-FileHash -LiteralPath $scene).Hash.ToLowerInvariant() -ne 'e0ea510c5a50981d0f53ab8c26045a25efccc1b2d8daa2a62901e8330d675c74') { throw 'Delivered scene changed; refuse asset updates.' }
$expected = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Evidence/New-assets-before-final-fixes.json') -Raw | ConvertFrom-Json
$updates = @()
foreach ($entry in $expected.PSObject.Properties) {
    if (-not $entry.Name.StartsWith('UI/Prefabs/Operator')) { throw 'Unexpected manifest path.' }
    $source = Join-Path $stage ($relativeRoot + '/' + $entry.Name)
    $target = Join-Path $workspace ($relativeRoot + '/' + $entry.Name)
    $before = (Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant()
    $after = (Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant()
    if ($before -ne $entry.Value -and $before -ne $after) { throw "Concurrent asset changes: $($entry.Name)" }
    if ($entry.Name.EndsWith('.meta') -and $before -ne $after) { throw 'Metadata must remain byte-identical.' }
    $updates += [pscustomobject]@{ path=$entry.Name; before=$before; after=$after; source=$source; target=$target }
}
foreach ($update in $updates) {
    if ($update.before -ne $update.after) { Copy-Item -LiteralPath $update.source -Destination $update.target -Force }
    if ((Get-FileHash -LiteralPath $update.target).Hash.ToLowerInvariant() -ne $update.after) { throw 'Final asset verification failed.' }
}
$updates | Select-Object path,before,after | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Evidence/New-asset-cleanup.json') -Encoding UTF8
Write-Output "Verified all $($updates.Count) new Phase 5 assets; updated $(($updates | Where-Object {$_.before -ne $_.after}).Count) prefab files. Scene and existing Phase 1-4 assets unchanged."
