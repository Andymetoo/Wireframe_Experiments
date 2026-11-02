AURORA R-54 MECHANICAL RECONCILIATION / APPLICATION PACKAGE (tested 2026-11-02)

What this is
  The tested result of reconciling the simulation with your finished authored machine. It was built
  from your current files (prefab saved 07:54, authored scene saved 09:06) in the isolated validation
  project; nothing in your real project has been changed. Full write-up:
  Package/Documentation/MechanicalReconciliation.txt (installed into Documentation/).

Contents
  Package/                     files to install, laid out relative to Assets/Experiments/IceResurfacer
  ORIGINALS.sha256             exact files the package replaces (it refuses if any changed since)
  PACKAGE.sha256               integrity of the package
  Apply-MechanicalReconciliation.ps1
  Evidence/                    test results, measurements, clearance audits, baselines, screenshots
                               (Shots/Before-* and After-* use identical machine-relative cameras)

Backups already made (verified checksums)
  Backups~/PreReconciliation_20261102_080115   prefab, meshes, materials, both scenes, recordings (08:01)
  Backups~/PreReconciliation_20261102_094853   the same after your 09:06 scene save
  The apply script makes a third, of exactly what it replaces.

APPLY (exact tested result)
  1. Close Unity. If you changed the machine or the authored scene since 09:06 today, read "If you
     edited since" below instead.
  2. PowerShell, in this folder:
        .\Apply-MechanicalReconciliation.ps1 -DryRun     (shows the plan, writes nothing)
        .\Apply-MechanicalReconciliation.ps1
     It replaces 17 files (prefab, authored scene, 6 runtime scripts, 6 test scripts, 2 docs, the
     isolated-run script), adds 11 (3 Editor scripts + .meta, 1 doc, 4 new calibrated GPS guides) and
     never overwrites an existing guide or recording.
  3. Open Unity, Experiments > Ice Resurfacer > Open Authored Scene. Select AuroraR54: MECHANICAL
     ALIGNMENT should list everything as "matches" except the board guide roller (see conflicts).
     Press Play: the conditioner stays where you put it, lowered and raised.
  4. GPS: Numpad 8 > Refresh > "Andy_Try_1 / recorded track (repaired) / authored Aurora calibration"
     > Use as GPS. The older Andy guides are rejected in this scene (DrawbarLength) and still work in
     the launch scene, where nothing changed.
  Undo everything: .\Apply-MechanicalReconciliation.ps1 -Rollback "<backup folder it printed>"

If you edited since (the script reports CONFLICT and changes nothing)
  .\Apply-MechanicalReconciliation.ps1 -CodeOnly   installs scripts, docs and guides but not the prefab
  or scene. Then in Unity, on your newer machine:
    a. Authored scene, select AuroraR54. If the Overrides menu lists Added GameObjects that duplicate
       prefab objects, revert those.
    b. MECHANICAL ALIGNMENT > Transfer Authored Geometry to Simulation... (one Undo step). It moves the
       lift clevises with the hitch, seats the blade, clears the lift linkage, aims the delivery throat
       into the tank opening, and writes hitch, tank hinge/scale and discharge into the scene calibration.
    c. "Add Board Brush Rig", select the arm, set "Brush contact radius" to the "Visual bristle radius"
       shown, "Apply Brush Geometry to Simulation...".
    d. Save. Overrides > Apply All so the prefab, not the scene, holds the edits.
    e. Training Route Editor > open each guide > Revalidate for active scene (save NEW copy). (The four
       guides in this package were validated against the tested calibration; if your transfer produced a
       different DrawbarLength they will be rejected, which is correct.)

Re-running the proof yourself (isolated copy only)
  ..\StageUnityValidation.ps1 -UpdateOnly, then RunIsolatedUnity.ps1 -Action ReconcileSurvey (baseline of the untouched model; run first) /
  ReconcileCapture /
  ReconcileApply / ReconcileVerify / ReconcileLaunch / ReconcileClearance, and -Action RunBatch with and
  without -Scene Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity.
