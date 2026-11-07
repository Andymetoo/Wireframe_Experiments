$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage = Join-Path $PSScriptRoot 'AcceptanceProject'
$lock = Join-Path $stage 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lock) {
    try { $handle = [IO.File]::Open($lock, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $handle.Dispose() }
    catch { throw 'Close isolated Unity before synchronizing source (lock is held).' }
}
$sourceRoot = Join-Path $workspace 'Assets/Experiments/IceResurfacer'
# Retain Unity-generated GUIDs, then synchronize only source code; authored test scenes/assets stay untouched.
Get-ChildItem -LiteralPath (Join-Path $stage 'Assets/Experiments/IceResurfacer/UI') -Filter '*.cs.meta' -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($stage.Length + 1)
    $destination = Join-Path $workspace $relative
    if (-not (Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath $_.FullName -Destination $destination }
}
$files = @(& rg --files $sourceRoot -g '*.cs' -g '*.cs.meta' -g '!**/*~/**')
foreach ($file in $files) {
    $target = Join-Path $stage $file.Substring($workspace.Length + 1)
    Copy-Item -LiteralPath $file -Destination $target -Force
}
Write-Output "Synchronized $($files.Count) source/metadata files; saved isolated scenes preserved."
