TEST ANXIETY — expanded short horror experience
SPOILER-FREE PLAYER AND TECHNICAL GUIDE

LAUNCH

Open Assets/Experiments/TestAnxiety/Scenes/TestAnxiety.unity in the project's
Unity 6000.4.1f1 Editor. Press Play, then BEGIN EXAMINATION.
The menu Test Anxiety > Open Prototype Scene opens the same saved scene.

The saved scene is a composition root with explicit material and question-data
references. It constructs the furnished environment in Awake. An empty-looking
Scene view before Play is expected. No generation command is required.
Test Anxiety > Recreate Prototype Scene reproduces the saved scene if needed.
It only saves this experiment's scene and preserves Unity's normal prompt for
unsaved work in the currently open scene.

This pass expands the complete examination flow, environmental staging, sound,
reading interactions, player decisions, and conclusion. It keeps the original
first-person observation, bubble sheets, and physical optical reader.

Intended first-play length: approximately 15–20 minutes at an exploratory pace.
This is a design target, not a measured playtest result. Reading and observation
set the pace; most interactions can be put down and resumed.

CONTROLS

W/A/S/D: walk. Mouse: look. Arrow keys: alternate look controls.
E: interact with the object named in the prompt; put down inspected paper.
Left click: fill answer bubbles, choose buttons, compare paper copies.
Mouse wheel: scroll longer documents.
R: inspect the retained copy once one has been collected.
H: briefly show a contextual hint when you need direction.
Escape: put down an inspection; in the room, pause/resume.

The pause menu contains mouse sensitivity, sound volume, optional sound
captions, and restart. Restart preserves these preferences but resets progress.
Stop Play Mode to leave. Progress lasts for this Play session only.

Sound is part of the experience; headphones are recommended. Sound captions are
optional, and important progression instructions also appear as readable text.
The same E interaction is used for additional context-specific actions.

PLAYING WITHOUT SPOILERS

Start at desk 014 in the center of the back row. Read the instructions on the
paper, observe the environment, and retain your copies. The reader accepts a
completed section even when responses are not credited. An unfinished paper
keeps its marks, and a returned sheet waits for collection. If you lower a
report before acknowledging it, interact with the reader or desk to reopen it.

The interface offers comparisons and previous returns. Use them when useful.
The H hint is optional; it explains the current required action without giving
later events away. Detailed design and walkthrough notes are separated into
SPOILERS.txt. Read that file after playing to preserve the first experience.

TECHNICAL STRUCTURE

Core/ExamCatalog.cs — validated, immutable copies of JSON definitions.
Core/ExamSession.cs — answers, result snapshots, prerequisites and progression.
Resources/TestAnxietyExam.json — authored examination content.
Presentation/TestAnxietyGame.cs — input, camera, scene composition.
Presentation/TestAnxietyGame.Sequence.cs — environmental and audio staging.
Presentation/TestAnxietyGame.UI.cs — booklet, reports, HUD, pause and conclusion.
Presentation/TestAnxietyGame.Documents.cs — readable document presentation.
Presentation/TestAnxietyClassroom*.cs — conventional solid URP environment.
Presentation/TestAnxietyScanner.cs — physical paper and reader state.
Presentation/TestAnxietyAudio.cs — synthesized spatial sounds and ambience.
Editor/TestAnxietyEditorTools.cs — explicit open/recreate/validate menu commands.

Existing game modes, launcher, Build Settings, packages, and shared graphics
assets were not changed. No branches, commits, or pushes were created.
Runtime materials, textures, meshes and audio sources belong to this scene.
The URP Lit material is explicitly retained in Resources. UI typography uses
Unity's existing IMGUI and LegacyRuntime font/TextMesh resources.

Authoring schema and event details are in SPOILERS.txt. Presentation references
stable question/section IDs; changing story-related IDs requires updating those
staging hooks. Ordinary questions remain data-driven and paginated.

VALIDATION

From the repository root:

  Push-Location ValidationTestAnxiety
  dotnet run --project TestAnxiety.Validation.csproj --configuration Release
  Pop-Location

Run in that directory so its global.json selects installed .NET 8. The suite
needs no downloaded NuGet packages. It checks immutable records, grading,
answer persistence, progression prerequisites, pause/resume timing inputs,
invalid transitions, alternate response paths, and malformed authoring data.
Latest result for this pass: 2,572 checks passed, zero failures.

  powershell -NoProfile -ExecutionPolicy Bypass -File ValidationTestAnxiety\CompileUnity.ps1

This invokes only the installed C# compiler against local Unity reference DLLs.
Scratch outputs go to Library/TestAnxietyValidation. It never starts Unity,
imports assets, enters Play Mode, or builds a player. It looks for the project's
exact Editor version first. Pass -UnityDataPath to select another local install.

In Unity, Test Anxiety > Validate Catalog and Progression runs imported-data
and full logical-progression checks. This command does not verify rendering.

The prior prototype was reported functional by the developer. This expansion
has automated logic validation and static runtime/Editor compilation; it has
not been run in Unity by the implementation agent. All ten runtime source files
and the Editor utility compile without warnings/errors against the project's
exact Unity 6000.4.1f1 assemblies at G:/UnityInstalls/6000.4.1f1/Editor/Data.
Asset metadata, GUID uniqueness, saved-scene references and assembly definitions
also pass static checks. Unity import, scene lifecycle, input, collision, visuals,
audio balance, performance, and first-play timing still need Editor playtesting.
Unity was not launched or controlled and no standalone build was made.

KNOWN LIMITATIONS

Meshes, surface textures and sounds are still procedural assets. There is no
recorded voice acting, animated hand/body model, external street environment,
or between-session save. Lighting is real time rather than baked. Added geometry
and lighting have not been profiled on target hardware. The readable paper
overlay takes priority over strict physical scale. Physical reader forms show
up to nine rows per section; the logical UI paginates larger sets.

Before distribution, test the complete sequence in the target Editor, both
1280x720 and 1920x1080 Game views, hardware audio, pause during reader motion,
put-down/reopen interactions, and restart. A detailed regression walkthrough
is included at the end of SPOILERS.txt for after the first playthrough.
