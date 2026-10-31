param(
    [ValidateSet('RunBatch','SpatialAudio','RoutePersistence','TrainingPersistence','RouteUXFresh','OperatorPrefsWrite','OperatorPrefsRead','RouteRepair','RouteRepairGui','RouteRepairPersistence','BuildLayoutPlayer','BuildPerformance')][string]$Action='RunBatch',
    [string]$LogName='Operator-ergonomics-unity-1.log'
)
$ErrorActionPreference='Stop'
$stage=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'UnityProject')).Path
$unity='G:\UnityInstalls\6000.4.1f1\Editor\Unity.exe'
$log=Join-Path $PSScriptRoot $LogName
$method=if($Action -eq 'BuildPerformance'){'IceResurfacer.Editor.IcePerformanceBuild.Build'}elseif($Action -eq 'SpatialAudio'){'IceResurfacer.Editor.IceSpatialAudioValidation.RunBatch'}elseif($Action -eq 'RoutePersistence'){'IceResurfacer.Editor.IceRouteRecordingValidation.VerifyPersisted'}else{'IceResurfacer.Editor.IceResurfacerEditor.'+$Action}
if($Action -eq 'TrainingPersistence'){$method='IceResurfacer.Editor.IceTrainingGuideValidation.VerifyPersisted'}
if($Action -eq 'RouteUXFresh'){$method='IceResurfacer.Editor.IceRouteUXValidation.VerifyFreshPlay'}
if($Action -eq 'OperatorPrefsWrite'){$method='IceResurfacer.Editor.IceHUDVideoSpeedValidation.WritePersistence'}
if($Action -eq 'OperatorPrefsRead'){$method='IceResurfacer.Editor.IceHUDVideoSpeedValidation.ReadPersistence'}
if($Action -eq 'RouteRepair'){$method='IceResurfacer.Editor.IceRouteRepairValidation.RunBatch'}
if($Action -eq 'RouteRepairGui'){$method='IceResurfacer.Editor.IceRouteRepairValidation.RunGuiBatch'}
if($Action -eq 'RouteRepairPersistence'){$method='IceResurfacer.Editor.IceRouteRepairValidation.VerifyPersisted'}
$arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$stage+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"'))
$child=Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Isolated Unity $Action started, owned pid=$($child.Id), log=$log"
$child.WaitForExit()
Write-Output "Isolated Unity $Action exit=$($child.ExitCode)"
exit $child.ExitCode
