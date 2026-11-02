AURORA R-54 RINK HEIGHTS, SERVICE GATE AND EDITABLE FILL PORTS / APPLICATION PACKAGE (tested 2026-11-02)

What this is
  The tested result, built from your files as of 13:39 today in the isolated validation project.
  Nothing in your real project has been changed. Full write-up, measurements and editing guide:
  Package/Documentation/RinkAndFillPorts.txt (installed into Documentation/).

Contents
  Package/                  files to install, relative to Assets/Experiments/IceResurfacer
  ORIGINALS.sha256          exact files it replaces (it refuses if any changed since 13:39)
  PACKAGE.sha256            integrity of the package
  Apply-RinkAndFillPorts.ps1
  Evidence/                 results-*.txt, rink-Before/After.txt measurements, Unity-results-*.txt,
                            Shots/ (Before-* and After-* use identical cameras)

Backups already made (verified checksums)
  Backups~/PreRinkAndFillPorts_20261102_133913   Arena, Machine, Scenes, recordings (13:39)
  The apply script makes another, of exactly what it replaces.

APPLY (exact tested result)
  1. Save and close Unity. (The script refuses while this project is open.)
  2. PowerShell, in this folder:
        .\Apply-RinkAndFillPorts.ps1 -DryRun      (shows the plan, writes nothing)
        .\Apply-RinkAndFillPorts.ps1
     It replaces 11 files: the rink section prefab and its meshes asset, AuroraR54.prefab, 5 scripts,
     2 docs and the isolated-run script. It adds 20 files: 7 scripts, 2 materials, 1 doc, each with
     its .meta. It does not touch any scene, NorthlineArena.prefab, recordings or guides.
  3. Open Unity, then Experiments > Ice Resurfacer > Open Authored Scene.
       The NorthlineArena Inspector drift report should show all checks within 2 mm.
       Open AuroraR54.prefab and select Water Fill Connections > Flood Fill Port. The Inspector
       service check shows reach and hose length, and the Scene view shows the gizmos.
       Press Play, take the nozzle, and E-couple it to each port.
  Undo everything: .\Apply-RinkAndFillPorts.ps1 -Rollback "<backup folder it printed>"
  (Rollback refuses if you edited an installed file afterwards. Add -Force only to discard those edits.)

If you edited since 13:39 (the script reports CONFLICT and changes nothing)
  .\Apply-RinkAndFillPorts.ps1 -CodeOnly   installs scripts and docs, but not the rink section,
  the machine prefab or the materials. Then in Unity, with the authored scene open:
    a. Experiments > Ice Resurfacer > Authored Scene > Refine Rink Boards and Glass...
       Backs up first; rebuilds the four band meshes in place, adds the edge channel and gate
       posts, raises the supports you have not moved.
    b. Experiments > Ice Resurfacer > Authored Scene > Add Persistent Water Fill Ports to Machine...
       Backs up first; adds Water Fill Connections at the generated positions and adopts a
       "FLOOD WATER fill coupling" / "WASH WATER fill coupling" if you made one.
    Both are one-time; running either again changes nothing. Undo: Advanced Recovery > Restore backup.

Re-running the proof yourself (isolated copy only)
  ..\StageUnityValidation.ps1 -UpdateOnly, then ..\RunIsolatedUnity.ps1 -Action RinkSurvey -RinkPrefix Before /
  RinkCaptureBefore / RinkApply / RinkSurvey -RinkPrefix After / RinkVerify / RinkMovePorts / RinkVerifyMoved /
  RinkEquivalence / RinkTankGlass, and -Action RunBatch with and without
  -Scene Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity.
  (RinkMovePorts deliberately moves the ports in the isolated prefab; restage before re-verifying.)
