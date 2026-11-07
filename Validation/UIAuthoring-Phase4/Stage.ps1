param([switch]$Refresh)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage = Join-Path $PSScriptRoot 'AcceptanceProject'
if (Test-Path (Join-Path $stage 'Temp/UnityLockfile')) { throw 'Isolated Unity project is open.' }
if ((Test-Path $stage) -and -not $Refresh) { throw 'Stage exists. Use Refresh only after its Unity exits.' }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($folder in @('Packages', 'ProjectSettings')) { Copy-Item -LiteralPath (Join-Path $workspace $folder) -Destination $stage -Recurse -Force }
$files = @(& rg --files (Join-Path $workspace 'Assets/Experiments/IceResurfacer') (Join-Path $workspace 'Assets/Settings') -g '!**/*~/**')
foreach ($file in $files) {
    $target = Join-Path $stage $file.Substring($workspace.Length + 1)
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $file -Destination $target -Force
}
$settingsPath = Join-Path $stage 'ProjectSettings/ProjectSettings.asset'
$settings = [IO.File]::ReadAllText($settingsPath)
$settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', '  companyName: AuroraValidation')
$settings = [regex]::Replace($settings, '(?m)^  productName:.*$', '  productName: UIAuthoringPhase4Isolated')
[IO.File]::WriteAllText($settingsPath, $settings)
[IO.File]::WriteAllText((Join-Path $stage 'UIAuthoringPhase4ValidationProject.txt'), 'Disposable Phase 4 validation only.')
Write-Output "Staged $($files.Count) assets in $stage"
