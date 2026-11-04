param(
    [ValidateSet('RunBatch','SpatialAudio','RoutePersistence','TrainingPersistence','RouteUXFresh','OperatorPrefsWrite','OperatorPrefsRead','RouteRepair','RouteRepairGui','RouteRepairPersistence','RouteCalibration','BuildLayoutPlayer','BuildPerformance','AuthoredBake','AuthoredVerify','AuthoredWriteRearrangement','AuthoredVerifyRearrangement','ArenaBaseline','ArenaSetUp','ArenaVerify','ArenaWriteEdits','ArenaVerifyEdits','ArenaRecovery','ProportionsSurvey','ProportionsApply','ProportionsVerify','BrushSetUp','BrushVerify','ReconcileSurvey','ReconcileCapture','ReconcileApply','ReconcileVerify','ReconcileLaunch','ReconcileClearance','RinkSurvey','RinkCaptureBefore','RinkApply','RinkVerify','RinkMovePorts','RinkVerifyMoved','RinkEquivalence','RinkTankGlass')][string]$Action='RunBatch',
    [string]$LogName='Operator-ergonomics-unity-1.log',
    # Optional scene for the existing validators, e.g. Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity
    [string]$Scene='',
    # Screenshot prefix for ReconcileCapture (Before or After)
    [string]$ReconcilePrefix='Before',
    # Measurement/screenshot prefix for RinkSurvey (Before or After)
    [string]$RinkPrefix='Before'
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
if($Action -eq 'RouteCalibration'){$method='IceResurfacer.Editor.IceRouteCalibrationValidation.RunBatch'}
if($Action -eq 'AuthoredBake'){$method='IceResurfacer.Editor.IceAuthoredMachineValidation.Bake'}
if($Action -eq 'AuthoredVerify'){$method='IceResurfacer.Editor.IceAuthoredMachineValidation.Verify'}
if($Action -eq 'AuthoredWriteRearrangement'){$method='IceResurfacer.Editor.IceAuthoredMachineValidation.WriteRearrangement'}
if($Action -eq 'AuthoredVerifyRearrangement'){$method='IceResurfacer.Editor.IceAuthoredMachineValidation.VerifyRearrangement'}
if($Action -eq 'ArenaBaseline'){$method='IceResurfacer.Editor.IceAuthoredArenaValidation.Baseline'}
if($Action -eq 'ArenaSetUp'){$method='IceResurfacer.Editor.IceAuthoredArenaValidation.SetUp'}
if($Action -eq 'ArenaVerify'){$method='IceResurfacer.Editor.IceAuthoredArenaValidation.Verify'}
if($Action -eq 'ArenaWriteEdits'){$method='IceResurfacer.Editor.IceAuthoredArenaValidation.WriteEdits'}
if($Action -eq 'ArenaVerifyEdits'){$method='IceResurfacer.Editor.IceAuthoredArenaValidation.VerifyEdits'}
if($Action -eq 'ArenaRecovery'){$method='IceResurfacer.Editor.IceAuthoredArenaValidation.Recovery'}
if($Action -like 'Proportions*'){$method='IceResurfacer.Editor.IceMachineProportionsValidation.'+$Action.Substring(11)}
if($Action -eq 'BrushSetUp'){$method='IceResurfacer.Editor.IceBrushConditionerValidation.SetUp'}
if($Action -eq 'BrushVerify'){$method='IceResurfacer.Editor.IceBrushConditionerValidation.Verify'}
if($Action -like 'Reconcile*'){$method='IceResurfacer.Editor.IceMechanicalReconciliationValidation.'+@{ReconcileCapture='Capture';ReconcileApply='Apply';ReconcileVerify='Verify';ReconcileLaunch='LaunchCompat'}[$Action]}
if($Action -eq 'ReconcileClearance'){$method='IceResurfacer.Editor.IceMechanicalSurvey.Clearance'}
if($Action -eq 'ReconcileSurvey'){$method='IceResurfacer.Editor.IceMechanicalSurvey.Survey'}
if($Action -eq 'RinkSurvey'){$method='IceResurfacer.Editor.IceRinkSurvey.Survey'}
if($Action -like 'Rink*' -and $Action -ne 'RinkSurvey'){$method='IceResurfacer.Editor.IceRinkFillPortValidation.'+$Action.Substring(4)}
$arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$stage+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"'))
if($Scene -ne ''){$arguments+=@('-iceScene',$Scene)}
if($Action -eq 'ReconcileCapture'){$arguments+=@('-reconcilePrefix',$ReconcilePrefix)}
if($Action -eq 'RinkSurvey'){$arguments+=@('-rinkPrefix',$RinkPrefix)}
$child=Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Isolated Unity $Action started, owned pid=$($child.Id), log=$log"
$child.WaitForExit()
Write-Output "Isolated Unity $Action exit=$($child.ExitCode)"
exit $child.ExitCode
