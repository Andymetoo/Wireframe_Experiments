param([ValidateSet('Persisted', 'Delivered')][string]$Variant)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage = Join-Path $PSScriptRoot 'AcceptanceProject'
$lockPath = Join-Path $stage 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    try { $handle = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $handle.Dispose() }
    catch { throw 'Isolated Unity still holds its project lock; wait for the owned process to exit.' }
}
$relativeScene = 'Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity'
$source = if ($Variant -eq 'Persisted') { Join-Path $stage 'ValidationResults/Authored-persistence.unity' } else { Join-Path $workspace $relativeScene }
$target = Join-Path $stage $relativeScene
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Scene copy hash mismatch.' }
Write-Output "Selected $Variant scene in the closed disposable project; working scene is unchanged."
