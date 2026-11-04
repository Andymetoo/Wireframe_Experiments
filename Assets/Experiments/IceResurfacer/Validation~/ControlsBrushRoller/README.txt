AURORA R-54 / CONTROLS, LIFT PREVIEW, BOARD BRUSH, BOARD CONTACT AND GUIDE ROLLER - APPLICATION PACKAGE
(tested 2026-11-02, finishing pass included)

What this is
  The tested result, built and validated in the isolated project (Validation~/UnityProject) from your
  current files: prefab saved 18:21, authored scene saved 09:51. Nothing in your real project has been
  changed. Meshes and materials are not part of this package and are never written. The machine prefab
  is: its only change is the guide roller assembly, moved 174 mm outboard so it leads the conditioner
  side plates by 25 mm, with its bracket lengthened to still meet the body (Evidence/prefab-changed-lines.txt
  lists every changed line). Full write-up: Package/Documentation/ControlsBrushRoller.txt.

Contents
  Package/            files to install, laid out relative to Assets/Experiments/IceResurfacer
  ORIGINALS.sha256    the exact files this package replaces (it refuses if any changed since the test),
                      including your prefab and authored scene
  REQUIRES.sha256     the machine meshes the scene's collision outline was fitted to
  PACKAGE.sha256      integrity of the package;  ADDED.txt  files it adds (never overwrites)
  Apply-ControlsBrushRoller.ps1
  Evidence/           results, before/after measurements, driving tables, suite runs, screenshots,
                      the plane-projection experiment

Backup already made (hash-verified, 452 files)
  Backups~/2026-11-02 182534 Before controls brush roller pass
    prefab + meta, meshes, materials, both scenes, arena, all scripts, docs, route recordings and guides.
  The apply script makes one more, of exactly what it replaces.

APPLY (exact tested result)
  1. Save your work and close Unity.
  2. PowerShell, in this folder:
        .\Apply-ControlsBrushRoller.ps1 -DryRun     (shows the plan, writes nothing)
        .\Apply-ControlsBrushRoller.ps1
     It replaces 26 files (19 scripts, the authored scene, the machine prefab, 2 docs, 3 validation files),
     adds 19 (7 scripts + their .meta, 1 doc + .meta, 1 validation file, 2 new GPS guides) and never
     overwrites a recording, a guide or anything it did not test.
  3. Open Unity, Experiments > Ice Resurfacer > Open Authored Scene, select AuroraR54:
     "Calibration source: Authored scene calibration (IceResurfacer_AuthoredMachine)"; every MECHANICAL
     ALIGNMENT item "matches"; the roller button reads "Guide roller leads the conditioner side plates".
     Open the prefab (Prefab Mode): the same, labelled "... Prefab Mode is read-only".
  4. Press Play. Hover/click/drag the flood and wash valves and the blade handwheel at any lift. Drive
     along the boards: the roller rides them and turns; the conditioner stays off.
  5. GPS: Numpad 8 > Refresh > "Andy_Try_1 / recorded track (repaired) / outline calibration (repaired
     15 mm)" or "AndyRoute / auto fit / outline calibration" > Use as GPS. Earlier guides are rejected in
     this scene with the new fields named; the launch scene is unchanged and keeps using them.
  Undo everything: .\Apply-ControlsBrushRoller.ps1 -Rollback "<backup folder it printed>"

If you edited since (the script reports CONFLICT and changes nothing)
  Scene or machine changed:  .\Apply-ControlsBrushRoller.ps1 -CodeOnly  (everything but the scene and the
    prefab; your newer files are kept), then in Unity on your newer machine: authored scene, select
    AuroraR54 > MECHANICAL ALIGNMENT:
      a. "Move Guide Roller Outboard N mm (moves your part)..." (lead field, default 25 mm; one Undo step;
         moves only the roller assembly and re-spans its bracket). Skip it if you placed the roller yourself.
      b. "Transfer Authored Geometry to Simulation..." (one Undo step): fits the collision outline and
         conditioner collider to the machine as drawn and moves the simulated roller to your roller.
    Save. Then Training Route Editor > open a guide > Revalidate for active scene (saves a NEW copy);
    a recorded track that touches the boards shows red regions to repair there.
  A script changed: the conflict names it. Compare it with Package/<same path> (every change is listed
    in ControlsBrushRoller.txt, FILES) and merge by hand, or send it back for an isolated re-run.

Re-running the proof yourself (isolated copy only)
  ..\StageUnityValidation.ps1 -UpdateOnly, then ..\RunIsolatedUnity.ps1
     -Action ControlsSurvey -SurveyPrefix Before   (read-only, before Apply)
     -Action ControlsApply                         (roller move + Inspector Transfer, GPS copies; 48 checks)
     -Action ControlsVerify                        (fresh process, 327 checks, screenshots)
     -Action RunBatch, and -Action RunBatch -Scene Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity
  From the repository root: dotnet run --project Assets/Experiments/IceResurfacer/Validation~/IceResurfacer.Validation.csproj
