$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$experiment=Join-Path $workspace 'Assets/Experiments/IceResurfacer'
$backup=Join-Path $experiment 'Backups~/UIAuthoring-Phase5-20261106'
if (Test-Path -LiteralPath $backup) { throw 'Backup already exists; inspect it before proceeding.' }
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$manifest=[ordered]@{}
$files=@(& rg --files $experiment -g '!**/*~/**')
foreach ($file in $files) {
    $relative=$file.Substring($experiment.Length+1).Replace('\','/')
    $destination=Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file -Destination $destination
    $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw "Backup mismatch: $relative" }
    $manifest[$relative]=$hash
}
[IO.File]::WriteAllText((Join-Path $backup 'baseline-sha256.json'),($manifest|ConvertTo-Json))
$routes=[ordered]@{}
Get-ChildItem -LiteralPath (Join-Path $experiment 'RouteRecordings~') -Recurse -File | ForEach-Object {
    $routes[$_.FullName.Substring($experiment.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()
}
if ($routes.Count -ne 31) { throw "Expected 31 routes, found $($routes.Count)" }
[IO.File]::WriteAllText((Join-Path $backup 'route-sha256.json'),($routes|ConvertTo-Json))
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'Evidence') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'Validation/UIAuthoring-Phase4/Unity-Phase5-initial-smoke.log') -Destination (Join-Path $PSScriptRoot 'Evidence/Initial-Phase1-to-4-smoke.log')
Copy-Item -LiteralPath (Join-Path $workspace 'Validation/UIAuthoring-Phase4/AcceptanceProject/ValidationResults/Phase4-results.txt') -Destination (Join-Path $PSScriptRoot 'Evidence/Initial-Phase1-to-4-results.txt')
Write-Output "Verified $($manifest.Count) experiment backup files and $($routes.Count) protected route hashes."
