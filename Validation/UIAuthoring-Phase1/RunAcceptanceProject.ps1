param(
    [string]$LogName = 'Unity-acceptance.log',
    [ValidateSet('RunBatch', 'VerifyPersisted')][string]$Method = 'RunBatch'
)
$ErrorActionPreference = 'Stop'
if ([IO.Path]::GetFileName($LogName) -ne $LogName) { throw 'LogName must be a file name.' }
$stageRoot = Join-Path $PSScriptRoot 'AcceptanceProject'
if (-not (Test-Path -LiteralPath (Join-Path $stageRoot 'UIAuthoringValidationProject.txt'))) { throw 'Stage the isolated project first.' }
$logPath = Join-Path $PSScriptRoot $LogName
if (Test-Path -LiteralPath $logPath) { throw 'Choose a new log name to preserve previous evidence.' }
$arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $stageRoot + '"'), '-executeMethod', ('IceResurfacer.UI.Editor.IceUIAuthoringValidation.' + $Method), '-logFile', ('"' + $logPath + '"'))
$child = Start-Process -FilePath 'G:\UnityInstalls\6000.4.1f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
[IO.File]::WriteAllText((Join-Path $PSScriptRoot ($LogName + '.pid')), [string]$child.Id)
Write-Output "Started owned isolated Unity acceptance pid=$($child.Id), log=$logPath"
if (-not $child.WaitForExit(900000)) {
    $child.Kill()
    $child.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot ($LogName + '.timeout')), 'Owned acceptance process timed out after 900 seconds.')
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot ($LogName + '.exit')), [string]$child.ExitCode)
Write-Output "Isolated Unity acceptance exit=$($child.ExitCode)"
exit $child.ExitCode
