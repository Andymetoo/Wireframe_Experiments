param([switch]$UpdateOnly)
$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$experiment=Join-Path $project 'Assets/Experiments/IceResurfacer'
$stage=Join-Path $PSScriptRoot 'Validation~/UnityProject'
New-Item -ItemType Directory -Force (Join-Path $stage 'Assets/Experiments/IceResurfacer') | Out-Null
if(!$UpdateOnly){
    foreach($folder in @('Packages','ProjectSettings')){ Copy-Item -LiteralPath (Join-Path $project $folder) -Destination $stage -Recurse -Force }
    Copy-Item -LiteralPath (Join-Path $project 'Assets/Settings') -Destination (Join-Path $stage 'Assets') -Recurse -Force
    New-Item -ItemType Directory -Force (Join-Path $stage 'UserSettings') | Out-Null
    [IO.File]::WriteAllText((Join-Path $stage 'UserSettings/Search.settings'),'{"indexOnEditorStartup":false}')
}
foreach($folder in @('Core','Presentation','Editor','Resources','Shaders','Scenes','Machine','Arena','RouteRecordings~')){
    if(Test-Path (Join-Path $experiment $folder)){
        Copy-Item -LiteralPath (Join-Path $experiment $folder) -Destination (Join-Path $stage 'Assets/Experiments/IceResurfacer') -Recurse -Force
        if(Test-Path (Join-Path $experiment ($folder+'.meta'))){Copy-Item -LiteralPath (Join-Path $experiment ($folder+'.meta')) -Destination (Join-Path $stage 'Assets/Experiments/IceResurfacer') -Force}
    }
}
Get-ChildItem -LiteralPath $experiment -File | Where-Object {$_.Extension -in '.asmdef','.meta'} | Copy-Item -Destination (Join-Path $stage 'Assets/Experiments/IceResurfacer') -Force
Write-Output "Isolated project: $stage"
