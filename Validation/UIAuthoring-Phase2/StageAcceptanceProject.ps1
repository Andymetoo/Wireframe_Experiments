param([switch]$Refresh, [switch]$ReusePhase1Cache)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$stageRoot = Join-Path $PSScriptRoot 'AcceptanceProject'
if ((Test-Path -LiteralPath $stageRoot) -and -not $Refresh) { throw 'AcceptanceProject already exists. Refresh only after its owned Unity process exits.' }
$openEditors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")
foreach ($process in $openEditors) {
    if ($process.CommandLine.Replace('/', '\') -like ('*' + $stageRoot.Replace('/', '\') + '*')) {
        throw "The Phase 2 isolated project is open in Unity pid $($process.ProcessId)."
    }
}
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
if ($ReusePhase1Cache) {
    $oldStage = Join-Path $repositoryRoot 'Validation/UIAuthoring-Phase1/AcceptanceProject'
    foreach ($process in $openEditors) {
        if ($process.CommandLine.Replace('/', '\') -like ('*' + $oldStage.Replace('/', '\') + '*')) {
            throw "The disposable Phase 1 source cache is in use by Unity pid $($process.ProcessId)."
        }
    }
    $sourceLibrary = (Resolve-Path -LiteralPath (Join-Path $oldStage 'Library')).Path
    $targetLibrary = [IO.Path]::GetFullPath((Join-Path $stageRoot 'Library'))
    $validationBoundary = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'Validation')) + [IO.Path]::DirectorySeparatorChar
    if (-not $sourceLibrary.StartsWith($validationBoundary, [StringComparison]::OrdinalIgnoreCase) -or
        -not $targetLibrary.StartsWith($validationBoundary, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Resolved cache paths must remain inside this workspace Validation directory.'
    }
    if (Test-Path -LiteralPath $targetLibrary) { throw 'Target Library exists; cache reuse must run only on a new project.' }
    # Transfer only a previously disposable cache; no live-project Library or durable evidence is changed.
    Move-Item -LiteralPath $sourceLibrary -Destination $targetLibrary
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'Cache-reuse.txt'), "Reused disposable Library from $sourceLibrary`nMoved to $targetLibrary`nPhase 1 durable Evidence remains unchanged.`n")
}
foreach ($folder in @('Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $folder) -Destination $stageRoot -Recurse -Force
}
$assetFiles = @(& rg --files (Join-Path $repositoryRoot 'Assets/Experiments/IceResurfacer') (Join-Path $repositoryRoot 'Assets/Settings') -g '!**/*~/**')
$assetFiles += @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'Assets') -File | Where-Object { $_.Name -like '*.inputactions*' } | ForEach-Object FullName)
foreach ($file in $assetFiles) {
    $relative = $file.Substring($repositoryRoot.Length + 1)
    $target = Join-Path $stageRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $file -Destination $target -Force
}
$settingsPath = Join-Path $stageRoot 'ProjectSettings/ProjectSettings.asset'
$settings = [IO.File]::ReadAllText($settingsPath)
$settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', '  companyName: AuroraValidation')
$settings = [regex]::Replace($settings, '(?m)^  productName:.*$', '  productName: UIAuthoringPhase2Isolated')
[IO.File]::WriteAllText($settingsPath, $settings)
New-Item -ItemType Directory -Path (Join-Path $stageRoot 'UserSettings') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $stageRoot 'UserSettings/Search.settings'), '{"indexOnEditorStartup":false}')
[IO.File]::WriteAllText((Join-Path $stageRoot 'UIAuthoringPhase2ValidationProject.txt'), 'Disposable isolated project for Phase 2 machinery console acceptance only.')
Write-Output "Isolated Phase 2 project staged: $stageRoot ($($assetFiles.Count) asset files)."
