param(
    [string]$LogName = 'Unity-acceptance.log',
    [ValidateSet('BuildNewAssets', 'RunBatch', 'VerifyPersisted', 'VerifyIntegrated')][string]$Method = 'RunBatch'
)
$ErrorActionPreference = 'Stop'
if ([IO.Path]::GetFileName($LogName) -ne $LogName) { throw 'LogName must be a file name.' }
$stageRoot = Join-Path $PSScriptRoot 'AcceptanceProject'
if (-not (Test-Path -LiteralPath (Join-Path $stageRoot 'UIAuthoringPhase2ValidationProject.txt'))) { throw 'Stage the marked Phase 2 isolated project first.' }
$logPath = Join-Path $PSScriptRoot $LogName
if (Test-Path -LiteralPath $logPath) { throw 'Choose a fresh log name to preserve prior evidence.' }
$executeMethod = if ($Method -eq 'BuildNewAssets') { 'IceResurfacer.Editor.IceMachineryConsoleFactory.BuildNewAssetsBatch' } else { 'IceResurfacer.UI.Editor.IceMachineryConsoleValidation.' + $Method }
$arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $stageRoot + '"'), '-executeMethod', $executeMethod, '-logFile', ('"' + $logPath + '"'))
$child = Start-Process -FilePath 'G:\UnityInstalls\6000.4.1f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
[IO.File]::WriteAllText((Join-Path $PSScriptRoot ($LogName + '.pid')), [string]$child.Id)
Write-Output "Started owned Phase 2 isolated Unity pid=$($child.Id), method=$Method, log=$logPath"
if (-not $child.WaitForExit(1200000)) {
    $child.Kill()
    $child.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot ($LogName + '.timeout')), 'Owned acceptance process timed out after 1200 seconds.')
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot ($LogName + '.exit')), [string]$child.ExitCode)
Write-Output "Phase 2 isolated Unity exit=$($child.ExitCode)"
exit $child.ExitCode
