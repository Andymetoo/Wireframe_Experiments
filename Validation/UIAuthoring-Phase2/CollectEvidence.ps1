param([Parameter(Mandatory=$true)][string]$AcceptanceLog, [Parameter(Mandatory=$true)][string]$PersistenceLog, [string]$IntegrationLog)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$stageRoot = Join-Path $PSScriptRoot 'AcceptanceProject'
$evidenceRoot = Join-Path $PSScriptRoot 'Evidence'
$logs = @($AcceptanceLog, $PersistenceLog)
if ($IntegrationLog) { $logs += $IntegrationLog }
foreach ($name in $logs) {
    if ([IO.Path]::GetFileName($name) -ne $name) { throw 'Log arguments must be file names.' }
    $exitPath = Join-Path $PSScriptRoot ($name + '.exit')
    if (-not (Test-Path -LiteralPath $exitPath) -or [IO.File]::ReadAllText($exitPath).Trim() -ne '0') { throw "Run did not complete successfully: $name" }
}
$sourceRoot = Join-Path $repositoryRoot 'Assets/Experiments/IceResurfacer'
$paths = @(& rg --files $sourceRoot -g '*.cs' -g '*.cs.meta' -g '*.asmdef' -g '*.asmdef.meta' -g '!**/*~/**')
$paths += @(& rg --files (Join-Path $sourceRoot 'UI') -g '!*.cs' -g '!*.cs.meta' -g '!*.asmdef' -g '!*.asmdef.meta')
$records = @()
foreach ($path in ($paths | Sort-Object -Unique)) {
    $relative = $path.Substring($repositoryRoot.Length + 1)
    $tested = Join-Path $stageRoot $relative
    if (-not (Test-Path -LiteralPath $tested)) { throw "Tested project is missing $relative" }
    $sourceHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $testedHash = (Get-FileHash -LiteralPath $tested -Algorithm SHA256).Hash
    if ($sourceHash -ne $testedHash) { throw "Source changed after test staging: $relative" }
    $records += [ordered]@{ path=$relative; sourceSHA256=$sourceHash; testedSHA256=$testedHash }
}
New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $stageRoot 'ValidationResults') -File)) {
    Copy-Item -LiteralPath $file.FullName -Destination $evidenceRoot -Force
}
foreach ($name in $logs) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $evidenceRoot -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ($name + '.exit')) -Destination $evidenceRoot -Force
}
$provenance = [ordered]@{
    utc=[DateTime]::UtcNow.ToString('o'); unity='6000.4.1f1'; project=$stageRoot;
    acceptanceLog=$AcceptanceLog; freshProcessPersistenceLog=$PersistenceLog; exactIntegratedSceneLog=$IntegrationLog;
    note='Every listed source/asset matches the final tested project. Authored test scene contains deliberate disposable overrides and is separately recorded; live scene integration is reviewed independently.';
    files=$records
}
if ($IntegrationLog) {
    $scene = 'Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity'
    $sourceHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot $scene) -Algorithm SHA256).Hash
    $testedHash = (Get-FileHash -LiteralPath (Join-Path $stageRoot $scene) -Algorithm SHA256).Hash
    if ($sourceHash -ne $testedHash) { throw 'Exact integrated scene differs from the final no-save Play verification.' }
    $provenance.integratedScene = [ordered]@{ path=$scene; sourceSHA256=$sourceHash; testedSHA256=$testedHash }
}
[IO.File]::WriteAllText((Join-Path $evidenceRoot 'Provenance.json'), ($provenance | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output "Preserved acceptance evidence and $($records.Count) matching source/asset hashes at $evidenceRoot"
