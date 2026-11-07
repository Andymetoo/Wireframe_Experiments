param([ValidateSet('Installed','Delivered','Edited')][string]$Variant='Installed')
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage=Join-Path $PSScriptRoot 'AcceptanceProject'
if (-not (Test-Path (Join-Path $stage 'UIAuthoringPhase4ValidationProject.txt'))) { throw 'Not a marked disposable project.' }
$lockPath=Join-Path $stage 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    try { $handle=[IO.File]::Open($lockPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None); $handle.Dispose() }
    catch { throw 'Wait for the owned Unity process to exit before selecting a test scene.' }
}
$relative='Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity'
$source=switch ($Variant) {
    'Installed' { Join-Path $stage 'ValidationResults/Installed-Phase4.unity' }
    'Edited' { Join-Path $stage 'ValidationResults/Authored-persistence.unity' }
    'Delivered' { Join-Path $workspace $relative }
}
$destination=Join-Path $stage $relative
Copy-Item -LiteralPath $source -Destination $destination -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Selected scene hash mismatch.' }
Write-Output "Selected $Variant scene; hash verified."
