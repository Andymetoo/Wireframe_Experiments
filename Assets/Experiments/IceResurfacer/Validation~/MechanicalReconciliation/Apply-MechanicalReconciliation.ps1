# Applies the tested Aurora R-54 mechanical reconciliation to this project (no rebake, no regeneration).
#   .\Apply-MechanicalReconciliation.ps1 -DryRun        checks only; prints the plan, writes nothing
#   .\Apply-MechanicalReconciliation.ps1                applies the exact tested prefab, scene, code, docs, new guides
#   .\Apply-MechanicalReconciliation.ps1 -CodeOnly      code, docs and new guides only (use when the prefab or
#                                                       scene changed since the reconciliation; then use the
#                                                       Inspector's Transfer Authored Geometry, see README.txt)
#   .\Apply-MechanicalReconciliation.ps1 -Rollback "<backup folder>"   restores a backup this script made
param([switch]$DryRun,[switch]$CodeOnly,[string]$Rollback='')
$ErrorActionPreference='Stop'
$pkg=$PSScriptRoot
$experiment=(Resolve-Path (Join-Path $pkg '..\..')).Path
$project=(Resolve-Path (Join-Path $experiment '..\..\..')).Path
$assets=@('Machine\AuroraR54.prefab','Scenes\IceResurfacer_AuthoredMachine.unity')

function Sha($path){ (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
# Package paths are relative to Assets/Experiments/IceResurfacer.
function Target($relative){ return Join-Path $experiment $relative }

# Unity must not have this project open: it would keep (and could re-save) the old scene in memory.
$escaped=[regex]::Escape($project).Replace('\\','[\\/]')
$open=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match $escaped -and $_.CommandLine -notmatch 'Validation~' })
if($open.Count -gt 0 -and -not $DryRun){ throw "Unity has this project open (pid $($open.ProcessId -join ', ')). Close it first. If you saved new prefab/scene edits, use -CodeOnly." }
if($open.Count -gt 0){ "NOTE: Unity has this project open; a real run would refuse until it is closed." }

if($Rollback -ne ''){
    $b=(Resolve-Path -LiteralPath $Rollback).Path
    foreach($line in Get-Content (Join-Path $b 'SHA256SUMS.txt')){ $h,$rel=$line -split '  ',2; if((Sha (Join-Path $b $rel)) -ne $h){ throw "Backup file changed: $rel" } }
    foreach($line in Get-Content (Join-Path $b 'SHA256SUMS.txt')){ $h,$rel=$line -split '  ',2; if(-not $DryRun){ Copy-Item -LiteralPath (Join-Path $b $rel) -Destination (Target $rel) -Force }; "restored $rel" }
    # Added files, and the .meta Unity generated for any of them.
    foreach($rel in Get-Content (Join-Path $b 'ADDED.txt')){ foreach($r in @($rel,"$rel.meta")){ $t=Target $r; if(Test-Path -LiteralPath $t){ if(-not $DryRun){ Remove-Item -LiteralPath $t -Force }; "removed added $r" } } }
    "Rollback complete$(if($DryRun){' (dry run)'})."; return
}

# 1. Package integrity.
foreach($line in Get-Content (Join-Path $pkg 'PACKAGE.sha256')){ $h,$rel=$line -split '  ',2; if((Sha (Join-Path $pkg "Package\$rel")) -ne $h){ throw "Package file damaged: $rel" } }

# 2. Every file to be replaced must still be exactly what was reconciled.
$replace=@();$conflicts=@()
foreach($line in Get-Content (Join-Path $pkg 'ORIGINALS.sha256')){
    $h,$rel=$line -split '  ',2
    if($CodeOnly -and $assets -contains $rel){ continue }
    $t=Target $rel
    if(-not (Test-Path -LiteralPath $t)){ $conflicts+="$rel is missing"; continue }
    if((Sha $t) -ne $h){ $conflicts+="$rel changed since the reconciliation" } else { $replace+=$rel }
}
if($conflicts.Count -gt 0){
    $conflicts | ForEach-Object { "CONFLICT $_" }
    throw "Nothing was changed. Prefab/scene edited since 2026-11-02 09:06? Run with -CodeOnly and use the Inspector (README.txt). Code edited? Merge by hand or re-run the isolated reconciliation."
}
$add=@()
foreach($line in Get-Content (Join-Path $pkg 'PACKAGE.sha256')){
    $h,$rel=$line -split '  ',2
    if($replace -contains $rel -or ($CodeOnly -and $assets -contains $rel)){ continue }
    if(Test-Path -LiteralPath (Target $rel)){
        if((Sha (Target $rel)) -eq $h){ continue }
        throw "Refusing to overwrite an existing file that is not part of the reconciliation: $rel"
    }
    $add+=$rel
}
"Will replace ($($replace.Count)):"; $replace | ForEach-Object { "  $_" }
"Will add ($($add.Count)):"; $add | ForEach-Object { "  $_" }
if($DryRun){ "Dry run: nothing written."; return }

# 3. Verified backup of everything replaced.
$backup=Join-Path $experiment ("Backups~\"+(Get-Date -Format 'yyyy-MM-dd HHmmss')+" Before mechanical reconciliation")
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
foreach($line in Get-Content (Join-Path $pkg 'PACKAGE.sha256')){ $h,$rel=$line -split '  ',2; if(($replace+$add) -contains $rel -and (Sha (Target $rel)) -ne $h){ throw "Copy verification failed: $rel. Restore with -Rollback `"$backup`"." } }
"Applied. Open Unity, then Experiments > Ice Resurfacer > Open Authored Scene. Undo with: -Rollback `"$backup`""
