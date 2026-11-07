$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage=Join-Path $PSScriptRoot 'AcceptanceProject'
if (-not (Test-Path (Join-Path $stage 'UIAuthoringPhase4ValidationProject.txt'))) { throw 'Missing disposable-project marker.' }
foreach ($process in Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
    $match=[regex]::Match($process.CommandLine,'(?i)-projectpath"?\s+"?([^"\r\n]+?)(?:"|\s+-|$)')
    if (-not $match.Success) { throw 'Unidentified Unity process; use the Editor installer instead.' }
    $openPath=[IO.Path]::GetFullPath($match.Groups[1].Value.Trim()).TrimEnd('\','/')
    if ($openPath -eq $workspace.TrimEnd('\','/')) { throw 'Working project is open; use Tools/Aurora/UI/Install Missing Phase 4 Screens.' }
    if ($openPath -eq $stage.TrimEnd('\','/')) { throw 'Wait for the disposable Unity process to exit.' }
}
if (Test-Path (Join-Path $workspace 'Temp/UnityLockfile')) { throw 'Working-project lock exists; do not overwrite serialized assets.' }
$relative='Assets/Experiments/IceResurfacer'
$backup=Join-Path $workspace ($relative+'/Backups~/UIAuthoring-Phase4-20261106/baseline-sha256.json')
$baseline=Get-Content -LiteralPath $backup -Raw | ConvertFrom-Json
$sceneRelative='Scenes/IceResurfacer_AuthoredMachine.unity'
$scene=Join-Path $workspace ($relative+'/'+$sceneRelative)
if ((Get-FileHash -LiteralPath $scene).Hash.ToLowerInvariant() -ne $baseline.$sceneRelative) { throw 'Scene changed since backup; refuse overwrite.' }
$source=Join-Path $stage ($relative+'/UI/Prefabs/Menus')
$target=Join-Path $workspace ($relative+'/UI/Prefabs/Menus')
$files=@(Get-ChildItem -LiteralPath $source -File -Recurse)
$files+=Get-Item -LiteralPath ($source+'.meta')
foreach ($file in $files) {
    $destination=Join-Path $workspace $file.FullName.Substring($stage.Length+1)
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Concurrent/new asset conflict: $destination" }
    }
}
foreach ($file in $files) {
    $destination=Join-Path $workspace $file.FullName.Substring($stage.Length+1)
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    if (-not (Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath $file.FullName -Destination $destination }
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Prefab copy verification failed.' }
}
& 'C:/Users/agaug/AppData/Local/Programs/Python/Python312/python.exe' (Join-Path $PSScriptRoot 'IntegrateScene.py') --apply
if ($LASTEXITCODE -ne 0) { throw 'Guarded scene integration did not complete.' }
Write-Output "Installed $($files.Count) new prefab/metadata files; no existing prefabs overwritten."
