param(
    [string]$UnityExecutable = 'G:\UnityInstalls\6000.4.1f1\Editor\Unity.exe',
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$ProbeName = 'startup-probe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Join-Path $PSScriptRoot ($ProbeName + '-Project')
$logPath = Join-Path $PSScriptRoot ('Unity-' + $ProbeName + '.log')
if (Test-Path -LiteralPath $logPath) { throw 'Probe log already exists. Preserve the previous evidence before running again.' }
foreach ($folder in @('Assets/Editor', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $projectRoot $folder) -Force | Out-Null
}
[IO.File]::WriteAllText((Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'), "m_EditorVersion: 6000.4.1f1`nm_EditorVersionWithRevision: 6000.4.1f1 (8535861f39e1)`n")
[IO.File]::WriteAllText((Join-Path $projectRoot 'Packages/manifest.json'), '{"dependencies":{}}')
[IO.File]::WriteAllText((Join-Path $projectRoot 'Assets/Editor/UIAuthoringStartupProbe.cs'), @'
using UnityEditor;
using UnityEngine;
public static class UIAuthoringStartupProbe
{
    public static void Run()
    {
        Debug.Log("UIAUTHORING_STARTUP_PROBE_REACHED: " + Application.unityVersion);
        EditorApplication.Exit(0);
    }
}
'@)
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'), '-executeMethod', 'UIAuthoringStartupProbe.Run', '-logFile', ('"' + $logPath + '"'))
$child = Start-Process -FilePath $UnityExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
[IO.File]::WriteAllText((Join-Path $PSScriptRoot ('Unity-' + $ProbeName + '.pid')), [string]$child.Id)
Write-Output "Started owned isolated Unity startup probe pid=$($child.Id), project=$projectRoot, log=$logPath"
if (-not $child.WaitForExit(240000)) {
    # This is the Process object returned by this invocation, never a discovered user process.
    $child.Kill()
    $child.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot ('Unity-' + $ProbeName + '.timeout')), 'Owned startup probe terminated after 240 seconds without completion.')
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot ('Unity-' + $ProbeName + '.exit')), [string]$child.ExitCode)
Write-Output "Isolated Unity startup probe exit=$($child.ExitCode)"
exit $child.ExitCode
