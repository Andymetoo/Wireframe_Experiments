param([switch]$Refresh)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$stageRoot = Join-Path $PSScriptRoot 'AcceptanceProject'
if ((Test-Path -LiteralPath $stageRoot) -and -not $Refresh) { throw 'AcceptanceProject already exists. Use -Refresh only when its owned Unity process has exited.' }
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
foreach ($folder in @('Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $folder) -Destination $stageRoot -Recurse -Force
}
# No Library, recorded routes, backup trees, or user settings are copied.
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
$settings = [regex]::Replace($settings, '(?m)^  productName:.*$', '  productName: UIAuthoringPhase1Isolated')
[IO.File]::WriteAllText($settingsPath, $settings)
New-Item -ItemType Directory -Path (Join-Path $stageRoot 'UserSettings') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $stageRoot 'UserSettings/Search.settings'), '{"indexOnEditorStartup":false}')
[IO.File]::WriteAllText((Join-Path $stageRoot 'UIAuthoringValidationProject.txt'), 'Disposable isolated project for UI authoring acceptance only.')
Write-Output "Isolated acceptance project staged: $stageRoot ($($assetFiles.Count) asset files); Library was not copied."
