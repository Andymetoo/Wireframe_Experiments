param(
    [ValidateSet('RunBatch','BuildLayoutPlayer','BuildPerformance')][string]$Action='RunBatch',
    [string]$LogName='Operator-ergonomics-unity-1.log'
)
$ErrorActionPreference='Stop'
$stage=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'UnityProject')).Path
$unity='G:\UnityInstalls\6000.4.1f1\Editor\Unity.exe'
$log=Join-Path $PSScriptRoot $LogName
$method=if($Action -eq 'BuildPerformance'){'IceResurfacer.Editor.IcePerformanceBuild.Build'}else{'IceResurfacer.Editor.IceResurfacerEditor.'+$Action}
$arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$stage+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"'))
$child=Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Isolated Unity $Action started, owned pid=$($child.Id), log=$log"
$child.WaitForExit()
Write-Output "Isolated Unity $Action exit=$($child.ExitCode)"
exit $child.ExitCode
