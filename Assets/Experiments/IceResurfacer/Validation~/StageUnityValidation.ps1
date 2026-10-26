param([switch]$UpdateOnly)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$stageRoot = Join-Path $PSScriptRoot 'UnityProject'
$experimentRoot = Join-Path $repositoryRoot 'Assets/Experiments/IceResurfacer'
$stageExperiment = Join-Path $stageRoot 'Assets/Experiments/IceResurfacer'
New-Item -ItemType Directory -Force $stageExperiment | Out-Null
if (!$UpdateOnly) {
    foreach ($name in @('ProjectSettings','Packages')) {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot $name) -Destination $stageRoot -Recurse -Force
    }
    $stageAssets = Join-Path $stageRoot 'Assets'
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Assets/Settings') -Destination $stageAssets -Recurse -Force
    New-Item -ItemType Directory -Force (Join-Path $stageRoot 'Library') | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Library/PackageCache') -Destination (Join-Path $stageRoot 'Library') -Recurse -Force
}
# This disposable project enters Play Mode before Unity's delayed Search startup.
# With no Search.index yet, Unity 6000.4.1f1 can enumerate an empty database list.
# Disable that unrelated Editor feature only in the staged project's preferences,
# including update-only runs. The real project's UserSettings are never touched.
$stageUserSettings = Join-Path $stageRoot 'UserSettings'
New-Item -ItemType Directory -Force $stageUserSettings | Out-Null
[IO.File]::WriteAllText((Join-Path $stageUserSettings 'Search.settings'), '{"indexOnEditorStartup":false}')
# Explicit folders avoid recursively copying Validation~/UnityProject into itself.
foreach ($name in @('Core','Presentation','Resources','Shaders','Scenes','Editor','Documentation')) {
    Copy-Item -LiteralPath (Join-Path $experimentRoot $name) -Destination $stageExperiment -Recurse -Force
    $metaPath = Join-Path $experimentRoot ($name+'.meta')
    if(Test-Path -LiteralPath $metaPath) { Copy-Item -LiteralPath $metaPath -Destination $stageExperiment -Force }
}
Get-ChildItem -LiteralPath $experimentRoot -File | Where-Object { $_.Name -ne '.gitignore' } |
    Copy-Item -Destination $stageExperiment -Force
Write-Output "Isolated Unity validation project ready: $stageRoot"
