# Applies the tested rink boards/glass refinement and persistent water fill ports (no rebake, no regeneration).
#   .\Apply-RinkAndFillPorts.ps1 -DryRun        checks only; prints the plan, writes nothing
#   .\Apply-RinkAndFillPorts.ps1                applies the exact tested rink section, machine prefab, code and docs
#   .\Apply-RinkAndFillPorts.ps1 -CodeOnly      code and docs only (use when the rink section or machine prefab
#                                               changed since 2026-11-02 13:39; then run the two menu items, README.txt)
#   .\Apply-RinkAndFillPorts.ps1 -Rollback "<backup folder>"   restores a backup this script made
#                                               (refuses if you edited an installed file since; -Force to discard those edits)
param([switch]$DryRun,[switch]$CodeOnly,[string]$Rollback='',[switch]$Force)
$ErrorActionPreference='Stop'
$pkg=$PSScriptRoot
$experiment=(Resolve-Path (Join-Path $pkg '..\..')).Path
$project=(Resolve-Path (Join-Path $experiment '..\..\..')).Path
# Authored assets: skipped by -CodeOnly (the menu items migrate your newer copies in place instead).
$assets=@('Arena\Sections\Rink boards and ice sheet.prefab','Arena\Sections\Rink boards and ice sheet.Meshes.asset','Machine\AuroraR54.prefab',
    'Machine\Materials\Flood water inlet - blue.mat','Machine\Materials\Flood water inlet - blue.mat.meta','Machine\Materials\Wash inlet - teal.mat','Machine\Materials\Wash inlet - teal.mat.meta')

function Sha($path){ (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
# Package paths are relative to Assets/Experiments/IceResurfacer.
function Target($relative){ return Join-Path $experiment $relative }
function Sums($file){ foreach($line in Get-Content -LiteralPath $file){ $h,$rel=$line -split '  ',2; [pscustomobject]@{Hash=$h;Path=$rel} } }

# Unity must not have this project open: it would keep (and could re-save) the old prefabs in memory.
$escaped=[regex]::Escape($project).Replace('\\','[\\/]')
$open=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match $escaped -and $_.CommandLine -notmatch 'Validation~' })
if($open.Count -gt 0 -and -not $DryRun){ throw "Unity has this project open (pid $($open.ProcessId -join ', ')). Save your work, close Unity, then run this again." }
if($open.Count -gt 0){ "NOTE: Unity has this project open; a real run would refuse until it is closed." }

if($Rollback -ne ''){
    $b=(Resolve-Path -LiteralPath $Rollback).Path
    foreach($s in Sums (Join-Path $b 'SHA256SUMS.txt')){ if((Sha (Join-Path $b $s.Path)) -ne $s.Hash){ throw "Backup file changed: $($s.Path)" } }
    # Never silently discard edits made after applying: every installed file must still be what was installed.
    $edited=@()
    foreach($s in Sums (Join-Path $b 'INSTALLED.txt')){ $t=Target $s.Path; if((Test-Path -LiteralPath $t) -and (Sha $t) -ne $s.Hash){ $edited+=$s.Path } }
    if($edited.Count -gt 0 -and -not $Force){ $edited | ForEach-Object { "EDITED SINCE APPLY $_" }; throw "Nothing was restored. These files changed after the package was applied; back them up yourself, then add -Force to discard those edits." }
    foreach($s in Sums (Join-Path $b 'SHA256SUMS.txt')){ if(-not $DryRun){ Copy-Item -LiteralPath (Join-Path $b $s.Path) -Destination (Target $s.Path) -Force }; "restored $($s.Path)" }
    # Added files, and the .meta for each of them.
    foreach($rel in Get-Content -LiteralPath (Join-Path $b 'ADDED.txt')){ foreach($r in @($rel,"$rel.meta")){ $t=Target $r; if(Test-Path -LiteralPath $t){ if(-not $DryRun){ Remove-Item -LiteralPath $t -Force }; "removed added $r" } } }
    "Rollback complete$(if($DryRun){' (dry run)'})."; return
}

# 1. Package integrity.
foreach($s in Sums (Join-Path $pkg 'PACKAGE.sha256')){ if((Sha (Join-Path $pkg "Package\$($s.Path)")) -ne $s.Hash){ throw "Package file damaged: $($s.Path)" } }

# 2. Every file to be replaced must still be exactly what was tested against (or already the packaged file).
$packaged=@{}; foreach($s in Sums (Join-Path $pkg 'PACKAGE.sha256')){ $packaged[$s.Path]=$s.Hash }
$replace=@();$conflicts=@()
foreach($s in Sums (Join-Path $pkg 'ORIGINALS.sha256')){
    if($CodeOnly -and $assets -contains $s.Path){ continue }
    $t=Target $s.Path
    if(-not (Test-Path -LiteralPath $t)){ $conflicts+="$($s.Path) is missing"; continue }
    $now=Sha $t
    if($now -eq $packaged[$s.Path]){ continue }
    if($now -ne $s.Hash){ $conflicts+="$($s.Path) changed since 2026-11-02 13:39" } else { $replace+=$s.Path }
}
if($conflicts.Count -gt 0){
    $conflicts | ForEach-Object { "CONFLICT $_" }
    throw "Nothing was changed. Rink section or machine prefab edited since? Run with -CodeOnly, then the two menu items (README.txt). Code or docs edited? Merge by hand."
}
$add=@()
foreach($s in Sums (Join-Path $pkg 'PACKAGE.sha256')){
    if($replace -contains $s.Path -or ($CodeOnly -and $assets -contains $s.Path)){ continue }
    if((Sums (Join-Path $pkg 'ORIGINALS.sha256')).Path -contains $s.Path){ continue }
    if(Test-Path -LiteralPath (Target $s.Path)){
        if((Sha (Target $s.Path)) -eq $s.Hash){ continue }
        throw "Refusing to overwrite an existing file that is not part of this package's originals: $($s.Path)"
    }
    $add+=$s.Path
}
"Will replace ($($replace.Count)):"; $replace | ForEach-Object { "  $_" }
"Will add ($($add.Count)):"; $add | ForEach-Object { "  $_" }
if($replace.Count+$add.Count -eq 0){ "Already applied: nothing to do."; return }
if($DryRun){ "Dry run: nothing written."; return }

# 3. Verified backup of everything replaced.
$backup=Join-Path $experiment ("Backups~\"+(Get-Date -Format 'yyyy-MM-dd HHmmss')+" Before rink and fill ports package")
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
$installed=@()
foreach($s in Sums (Join-Path $pkg 'PACKAGE.sha256')){ if(($replace+$add) -contains $s.Path){ if((Sha (Target $s.Path)) -ne $s.Hash){ throw "Copy verification failed: $($s.Path). Restore with -Rollback `"$backup`"." }; $installed+=($s.Hash+"  "+$s.Path) } }
$installed | Set-Content -Encoding ascii (Join-Path $backup 'INSTALLED.txt')
"Applied $($replace.Count) replaced + $($add.Count) added. Open Unity, then Experiments > Ice Resurfacer > Open Authored Scene."
if($CodeOnly){ "Then run Authored Scene > Refine Rink Boards and Glass... and Authored Scene > Add Persistent Water Fill Ports to Machine... (README.txt)." }
"Undo with: .\Apply-RinkAndFillPorts.ps1 -Rollback `"$backup`""
