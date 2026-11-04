# Installs the tested controls / lift preview / board brush / board contact / guide roller work into this project.
# Nothing is rebaked; meshes and materials are never written. The machine prefab is replaced only by the tested
# copy whose sole change is the guide roller assembly (moved outboard, bracket lengthened), and only if your
# prefab is still exactly the one tested.
#   .\Apply-ControlsBrushRoller.ps1 -DryRun        checks only; prints the plan, writes nothing
#   .\Apply-ControlsBrushRoller.ps1                code, docs, tests, new GPS guide copies AND the tested authored
#                                                  scene and machine prefab
#   .\Apply-ControlsBrushRoller.ps1 -CodeOnly      everything except the authored scene and the prefab (use when
#                                                  either changed since the test; then the Inspector steps, README)
#   .\Apply-ControlsBrushRoller.ps1 -Rollback "<backup folder>"   restores a backup this script made
param([switch]$DryRun,[switch]$CodeOnly,[string]$Rollback='')
$ErrorActionPreference='Stop'
$pkg=$PSScriptRoot
$experiment=(Resolve-Path (Join-Path $pkg '..\..')).Path
$project=(Resolve-Path (Join-Path $experiment '..\..\..')).Path
$scene='Scenes\IceResurfacer_AuthoredMachine.unity'
$prefab='Machine\AuroraR54.prefab'

function Sha($path){ (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Target($relative){ return Join-Path $experiment $relative }
function Lines($name){ Get-Content -LiteralPath (Join-Path $pkg $name) | Where-Object { $_ -ne '' } }

# Unity must not have this project open: it could keep (and re-save) the old scene, or reload mid-copy.
$escaped=[regex]::Escape($project).Replace('\\','[\\/]')
$open=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match $escaped -and $_.CommandLine -notmatch 'Validation~' })
if($open.Count -gt 0 -and -not $DryRun){ throw "Unity has this project open (pid $($open.ProcessId -join ', ')). Save your work, close Unity, then run this again." }
if($open.Count -gt 0){ "NOTE: Unity has this project open; a real run would refuse until it is closed." }

if($Rollback -ne ''){
    $b=(Resolve-Path -LiteralPath $Rollback).Path
    foreach($line in Get-Content (Join-Path $b 'SHA256SUMS.txt')){ $h,$rel=$line -split '  ',2; if((Sha (Join-Path $b $rel)) -ne $h){ throw "Backup file changed: $rel" } }
    foreach($line in Get-Content (Join-Path $b 'SHA256SUMS.txt')){ $h,$rel=$line -split '  ',2; if(-not $DryRun){ Copy-Item -LiteralPath (Join-Path $b $rel) -Destination (Target $rel) -Force }; "restored $rel" }
    foreach($rel in Get-Content (Join-Path $b 'ADDED.txt')){ foreach($r in @($rel,"$rel.meta")){ $t=Target $r; if(Test-Path -LiteralPath $t){ if(-not $DryRun){ Remove-Item -LiteralPath $t -Force }; "removed added $r" } } }
    "Rollback complete$(if($DryRun){' (dry run)'})."; return
}

# 1. Package integrity.
foreach($line in Lines 'PACKAGE.sha256'){ $h,$rel=$line -split '  ',2; if((Sha (Join-Path $pkg "Package\$rel")) -ne $h){ throw "Package file damaged: $rel" } }

# 2. Every file to be replaced must still be exactly what was tested; the scene also needs the same machine.
$replace=@();$conflicts=@()
foreach($line in Lines 'ORIGINALS.sha256'){
    $h,$rel=$line -split '  ',2
    if($CodeOnly -and ($rel -eq $scene -or $rel -eq $prefab)){ continue }
    $t=Target $rel
    if(-not (Test-Path -LiteralPath $t)){ $conflicts+="$rel is missing"; continue }
    if((Sha $t) -ne $h){ $conflicts+="$rel changed since the test" } else { $replace+=$rel }
}
if(-not $CodeOnly){
    foreach($line in Lines 'REQUIRES.sha256'){ $h,$rel=$line -split '  ',2; if((Sha (Target $rel)) -ne $h){ $conflicts+="$rel changed since the test (the scene's collision outline was fitted to the tested machine)" } }
}
if($conflicts.Count -gt 0){
    $conflicts | ForEach-Object { "CONFLICT $_" }
    throw "Nothing was changed. If only the authored scene or the machine changed, run with -CodeOnly and follow README.txt (your newer prefab and scene are kept). If a script changed, merge by hand (README.txt lists every file)."
}
$add=@()
foreach($rel in Lines 'ADDED.txt'){
    if(Test-Path -LiteralPath (Target $rel)){
        $expected=(Lines 'PACKAGE.sha256' | Where-Object { ($_ -split '  ',2)[1] -eq $rel }) -split '  ',2
        if((Sha (Target $rel)) -eq $expected[0]){ continue }
        throw "Refusing to overwrite an existing file that is not part of this package: $rel"
    }
    $add+=$rel
}
"Will replace ($($replace.Count)):"; $replace | ForEach-Object { "  $_" }
"Will add ($($add.Count)):"; $add | ForEach-Object { "  $_" }
if($CodeOnly){ "Not installed (-CodeOnly): $scene and $prefab. Use the Inspector roller move and Transfer afterwards (README.txt)." }
if($DryRun){ "Dry run: nothing written."; return }

# 3. Verified backup of everything replaced (and their .meta files).
$backup=Join-Path $experiment ("Backups~\"+(Get-Date -Format 'yyyy-MM-dd HHmmss')+" Before controls brush roller package")
New-Item -ItemType Directory -Force $backup | Out-Null
$sums=@()
foreach($rel in $replace){
    foreach($r in @($rel,"$rel.meta")){ $t=Target $r; if(Test-Path -LiteralPath $t){ $d=Join-Path $backup $r; New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null; Copy-Item -LiteralPath $t -Destination $d -Force; $sums+=((Sha $d)+"  $r") } }
}
$sums | Set-Content -Encoding ascii (Join-Path $backup 'SHA256SUMS.txt')
$add | Set-Content -Encoding ascii (Join-Path $backup 'ADDED.txt')
foreach($line in $sums){ $h,$r=$line -split '  ',2; if((Sha (Target $r)) -ne $h){ throw "Backup verification failed for $r; nothing was changed." } }
"Backup: $backup"

# 4. Copy, then verify every written file.
foreach($rel in $replace+$add){ $t=Target $rel; New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null; Copy-Item -LiteralPath (Join-Path $pkg "Package\$rel") -Destination $t -Force }
foreach($line in Lines 'PACKAGE.sha256'){ $h,$rel=$line -split '  ',2; if(($replace+$add) -contains $rel -and (Sha (Target $rel)) -ne $h){ throw "Copy verification failed: $rel. Restore with -Rollback `"$backup`"." } }
"Applied. Open Unity, then Experiments > Ice Resurfacer > Open Authored Scene. Undo with: -Rollback `"$backup`""
