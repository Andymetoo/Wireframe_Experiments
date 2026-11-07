param([string]$Method = 'BuildBatch', [string]$LogName = 'Unity-build.log')
$ErrorActionPreference = 'Stop'
$stage = Join-Path $PSScriptRoot 'AcceptanceProject'
if (-not (Test-Path (Join-Path $stage 'UIAuthoringPhase3ValidationProject.txt'))) { throw 'Stage first.' }
$lockPath = Join-Path $stage 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    try { $handle = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $handle.Dispose() }
    catch { throw 'Wait for the previous owned Unity process to exit before starting another run.' }
}
if ([IO.Path]::GetFileName($LogName) -ne $LogName) { throw 'LogName must be a filename.' }
$log = Join-Path $PSScriptRoot $LogName
if (Test-Path $log) { throw 'Use a new log filename.' }
$class = if ($Method -in @('BuildBatch','CompleteBatch')) { 'IceResurfacer.Editor.IceNavigationFactory' } else { 'IceResurfacer.Editor.IceNavigationValidation' }
$arguments = @('-batchmode','-force-d3d11','-projectPath',('"'+$stage+'"'),'-executeMethod',($class+'.'+$Method),'-logFile',('"'+$log+'"'))
$child = Start-Process -FilePath 'G:/UnityInstalls/6000.4.1f1/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
[IO.File]::WriteAllText(($log+'.pid'),[string]$child.Id)
Write-Output "Owned isolated Unity pid=$($child.Id), method=$Method"
while (-not $child.WaitForExit(1000)) { }
[IO.File]::WriteAllText(($log+'.exit'),[string]$child.ExitCode)
Write-Output "Unity exit=$($child.ExitCode)"
exit $child.ExitCode
