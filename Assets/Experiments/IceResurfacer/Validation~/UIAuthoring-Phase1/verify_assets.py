"""Read-only structural checks for Aurora Phase 1 authored assets (Python standard library).

These are serialized/source checks, not evidence of Unity import, rendering,
native mouse operation, Play Mode, or authoring persistence. Run from anywhere:
    python verify_assets.py
"""
from __future__ import annotations

import hashlib
import json
import math
import os
from pathlib import Path
import re
import sys

EXPERIMENT = Path(__file__).resolve().parents[2]
PROJECT = EXPERIMENT.parents[2]
UI = EXPERIMENT / "UI"
BACKUP = EXPERIMENT / "Backups~/UIAuthoring-Phase1-20261106"
DOC = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?\n", re.M)
REF = re.compile(r"\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*\d+)?\}", re.I)
BUILTIN_GUIDS = {"0000000000000000e000000000000000", "0000000000000000f000000000000000"}
EXPECTED_CHANGES = {
    "IceResurfacer.Runtime.asmdef", "Presentation/IceDashboard.cs",
    "Presentation/IceResurfacerGame.cs", "Presentation/IceWaterService.cs",
    "Scenes/IceResurfacer_AuthoredMachine.unity",
}
checks = 0


def require(condition: bool, message: str) -> None:
    global checks
    if not condition:
        raise AssertionError(message)
    checks += 1


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def asset_guid(path: Path) -> str:
    return re.search(r"^guid: ([0-9a-f]{32})$", read(Path(str(path) + ".meta")), re.M)[1]


def docs(text: str) -> dict[int, tuple[int, str]]:
    matches = list(DOC.finditer(text))
    result = {}
    for index, match in enumerate(matches):
        ident = int(match[2])
        require(ident not in result, f"Duplicate serialized object ID {ident}")
        result[ident] = (int(match[1]), text[match.end():matches[index+1].start() if index+1 < len(matches) else len(text)])
    return result


def field(text: str, key: str) -> str:
    match = re.search(r"^  " + re.escape(key) + r":\s*([^\n]+)", text, re.M)
    require(match is not None, f"Missing serialized field {key}")
    return match[1].strip()


def local(text: str, key: str) -> int:
    return int(REF.fullmatch(field(text, key))[1])


def vector(text: str, key: str) -> tuple[float, float]:
    value = field(text, key)
    match = re.fullmatch(r"\{x: ([\d.eE+\-]+), y: ([\d.eE+\-]+)\}", value)
    require(match is not None, f"Invalid {key}: {value}")
    return float(match[1]), float(match[2])


def resolve_guids() -> dict[str, Path]:
    result = {}
    packages = PROJECT / "Library/PackageCache"
    # Only these installed packages own scripts referenced by the pilot. Avoid
    # scanning unrelated package samples and editor tool artwork.
    bases = [PROJECT / "Assets"] + list(packages.glob("com.unity.ugui@*")) + list(packages.glob("com.unity.inputsystem@*"))
    for base in bases:
        for directory, names, files in os.walk(base):
            names[:] = [name for name in names if not name.endswith("~") and not name.startswith(".")]
            for filename in files:
                if not filename.endswith(".meta"):
                    continue
                path = Path(directory) / filename
                match = re.search(r"^guid: ([0-9a-f]{32})$", read(path), re.M)
                if not match:
                    continue
                asset = Path(str(path)[:-5])
                if match[1] in result and (UI in asset.parents or UI in result[match[1]].parents):
                    raise AssertionError(f"UI GUID collision: {asset}, {result[match[1]]}")
                result[match[1]] = asset
    return result


def verify_references(paths: list[Path], lookup: dict[str, Path]) -> None:
    cached = {}
    for path in paths:
        text = read(path)
        objects = docs(text)
        for match in REF.finditer(text):
            ident, guid = int(match[1]), match[2]
            if ident == 0:
                continue
            if not guid:
                require(ident in objects, f"Dangling local fileID {ident} in {path}")
                continue
            if guid in BUILTIN_GUIDS:
                continue
            require(guid in lookup, f"Unresolved GUID {guid} in {path}")
            asset = lookup[guid]
            require(asset.exists(), f"Missing referenced asset {asset}")
            if asset.suffix == ".asset":
                require(re.search(r"type:\s*2\}", match[0]) is not None,
                        f"Native serialized asset reference must use type 2: {asset} in {path}")
            if asset.suffix in {".asset", ".prefab"}:
                if asset not in cached:
                    cached[asset] = docs(read(asset))
                require(ident in cached[asset] or ident == 100100000 and asset.suffix == ".prefab",
                        f"Missing external object {ident} in {asset}, referenced by {path}")
            else:
                expected = {".cs": {11500000}, ".shader": {4800000}, ".ttf": {12800000},
                            ".txt": {4900000}, ".png": {21300000, 2800000}}.get(asset.suffix.lower())
                if expected is not None:
                    require(ident in expected, f"Unexpected importer fileID {ident} for {asset}")
    print(f"PASS GUID and object references: {len(paths)} UI serialized assets")


def verify_header(lookup: dict[str, Path]) -> None:
    main = UI / "Prefabs/Aurora UI.prefab"
    objects = docs(read(main))
    widget_guid = asset_guid(UI / "Runtime/IceHudWidget.cs")
    hud_guid = asset_guid(UI / "Runtime/IceAuthoredHud.cs")
    style_guid = asset_guid(UI / "Runtime/IceUIStyle.cs")
    scripts = {ident: REF.fullmatch(field(body, "m_Script"))[2]
               for ident, (kind, body) in objects.items() if kind == 114}
    widgets = {int(field(body, "kind")): (ident, body) for ident, (_, body) in objects.items()
               if scripts.get(ident) == widget_guid}
    require(set(widgets) == set(range(8)), "Top HUD must contain all eight unique widget kinds")
    require(sum(script == widget_guid for script in scripts.values()) == 8, "Unexpected duplicate widget")
    controllers = [(ident, body) for ident, (_, body) in objects.items() if scripts.get(ident) == hud_guid]
    require(len(controllers) == 1, "Top HUD must have one controller")
    controller_id, controller = controllers[0]
    widget_list = re.search(r"^  widgets:\n((?:  - .*\n)+)", controller, re.M)
    bound = {int(match[1]) for match in REF.finditer(widget_list[1])}
    require(bound == {ident for ident, _ in widgets.values()}, "Controller widget list must bind all eight indicators")
    require(local(controller, "game") == 0, "Reusable template should accept scene's explicit game override")
    require(objects[local(controller, "canvas")][0] == 223, "Controller Canvas reference is invalid")
    require(objects[local(controller, "visibility")][0] == 225, "Controller CanvasGroup reference is invalid")
    ray_script = scripts[local(controller, "raycaster")]
    require(lookup[ray_script].name == "GraphicRaycaster.cs", "Controller raycaster is not uGUI")

    texts = 0
    blockers = []
    for ident, script in scripts.items():
        body = objects[ident][1]
        name = lookup[script].name
        if name == "TextMeshProUGUI.cs":
            texts += 1
            require(field(body, "m_RaycastTarget") == "0", "Decorative text must not raycast")
            font = REF.fullmatch(field(body, "m_fontAsset"))
            material = REF.fullmatch(field(body, "m_sharedMaterial"))
            require(font[2] == material[2], "TMP font/material reference mismatch")
            require(lookup[font[2]].name == "LiberationSans SDF.asset", "Unexpected initial TMP font")
        elif name == "Image.cs" and field(body, "m_RaycastTarget") == "1":
            blockers.append(local(body, "m_GameObject"))
        elif script == style_guid:
            require(field(body, "applyFontSize") == "0", "Shared font sizes must be opt-in for authored pilot")
    require(texts >= 20, "Pilot has insufficient separately editable text objects")
    require(set(blockers) == {local(body, "m_GameObject") for _, body in widgets.values()},
            "Only the eight widget backgrounds may block the pointer; no fullscreen root target")

    expected_fill = {2: .42, 3: 1, 4: 1, 5: .25}
    for kind, (_, widget) in widgets.items():
        for key in ("label", "value"):
            target = local(widget, key)
            require(lookup[scripts[target]].name == "TextMeshProUGUI.cs", f"Widget {kind} needs TMP {key}")
        if kind in expected_fill:
            fill = objects[local(widget, "fill")][1]
            require(field(fill, "m_Type") == "3" and field(fill, "m_FillMethod") == "0", "Meter must use horizontal filled Image")
            require(math.isclose(float(field(fill, "m_FillAmount")), expected_fill[kind], abs_tol=1e-5),
                    f"Preview fill for widget {kind} disagrees with representative value")
        if kind == 2:
            require(lookup[scripts[local(widget, "action")]].name == "Button.cs", "Load diagnostics action must bind a Button")
        else:
            require(local(widget, "action") == 0, "Unexpected simulator action on a passive widget")

    scalers = [objects[ident][1] for ident, script in scripts.items() if lookup[script].name == "CanvasScaler.cs"]
    require(len(scalers) == 1, "One CanvasScaler required")
    require(field(scalers[0], "m_UiScaleMode") == "1" and vector(scalers[0], "m_ReferenceResolution") == (1600, 900),
            "Scale With Screen Size must use 1600x900")
    require(field(scalers[0], "m_ScreenMatchMode") == "1", "Expand mode preserves the reference layout within viewport")
    rects_by_go = {local(body, "m_GameObject"): body for klass, body in objects.values() if klass == 224}
    for _, widget in widgets.values():
        rect = rects_by_go[local(widget, "m_GameObject")]
        require(vector(rect, "m_AnchorMin") == vector(rect, "m_AnchorMax") == vector(rect, "m_Pivot") == (0, 1),
                "Pilot initial widget anchors should be independent at top-left")
        x, y = vector(rect, "m_AnchoredPosition")
        width, height = vector(rect, "m_SizeDelta")
        for screen_width, screen_height in ((1366, 768), (1600, 900), (1920, 1080)):
            scale = min(screen_width / 1600, screen_height / 900)
            require(x >= 0 and y <= 0 and (x+width)*scale <= screen_width and (-y+height)*scale <= screen_height,
                    f"Widget begins outside {screen_width}x{screen_height} viewport")
    events = [objects[ident][1] for ident, script in scripts.items() if lookup[script].name == "EventSystem.cs"]
    require(len(events) == 1 and field(events[0], "m_sendNavigationEvents") == "0", "Navigation must not consume driving keyboard focus")
    require(sum(lookup[script].name == "InputSystemUIInputModule.cs" for script in scripts.values()) == 1,
            "One Input System UI module required")

    scene = read(EXPERIMENT / "Scenes/IceResurfacer_AuthoredMachine.unity")
    game_id = re.search(r"propertyPath: game\s+value:\s*\n\s*objectReference: \{fileID: (\d+)\}", scene)
    require(game_id is not None, "Authored scene must explicitly bind its existing game")
    scene_objects = docs(scene)
    game_script = REF.fullmatch(field(scene_objects[int(game_id[1])][1], "m_Script"))[2]
    require(game_script == asset_guid(EXPERIMENT / "Presentation/IceResurfacerGame.cs"), "HUD scene override targets wrong simulator")
    require(f"target: {{fileID: {controller_id}, guid: {asset_guid(main)}, type: 3}}" in scene,
            "Scene game override must target the prefab HUD controller")
    print(f"PASS eight widgets, {texts} TMP labels, live binding references, preview values, raycast targets and initial three-resolution bounds")


def verify_preservation() -> None:
    baseline = json.loads(read(BACKUP / "baseline-sha256.json"))
    changed = set()
    for relative, expected in baseline.items():
        path = EXPERIMENT / relative
        require(path.exists(), f"Baseline asset deleted: {relative}")
        actual = hashlib.sha256(path.read_bytes()).hexdigest().upper()
        if actual != expected.upper():
            changed.add(relative)
    require(changed == EXPECTED_CHANGES, f"Unexpected existing-file change set: {sorted(changed ^ EXPECTED_CHANGES)}")
    for path in BACKUP.rglob("*"):
        if not path.is_file() or path.name == "baseline-sha256.json":
            continue
        relative = path.relative_to(BACKUP).as_posix()
        if relative not in EXPECTED_CHANGES:
            require((EXPERIMENT / relative).read_bytes() == path.read_bytes(), f"Backed-up protected asset changed: {relative}")
    scene_path = "Scenes/IceResurfacer_AuthoredMachine.unity"
    current = (EXPERIMENT / scene_path).read_bytes()
    before = (BACKUP / scene_path).read_bytes()
    eol = b"\r\n" if b"\r\n" in before else b"\n"
    stripped = re.sub(rb"(?m)^--- !u!1001 &1905400000\r?\n.*?(?=^--- !u!1660057539)", b"", current, flags=re.S)
    stripped = stripped.replace(b"  - {fileID: 1905400001}" + eol, b"")
    require(stripped == before, "Authored scene differs beyond new UI prefab instance and scene-root entry")
    require(asset_guid(EXPERIMENT / scene_path) == asset_guid(BACKUP / scene_path), "Authored scene GUID changed")
    dashboard = read(EXPERIMENT / "Presentation/IceDashboard.cs")
    require("if(!game.HasAuthoredHud)Header(game);" in dashboard, "Legacy header must be selectively suppressed")
    require("else if(new Rect(408,24,1164,108).Contains(p))return true;" in dashboard,
            "Legacy header rectangle must remain fallback-only")
    require("game.AuthoredHud.ContainsPointer(screenPoint)" in dashboard, "Migrated pointer ownership must use uGUI")
    print(f"PASS preservation: {len(baseline)} baseline hashes checked; only five intended files changed; scene byte preservation verified")


def main() -> None:
    lookup = resolve_guids()
    paths = sorted(UI.rglob("*.prefab")) + sorted(UI.rglob("*.asset"))
    verify_references(paths, lookup)
    verify_header(lookup)
    verify_preservation()
    font_path = UI / "Typography/Fonts/LiberationSans.ttf"
    require(font_path.read_bytes()[:4] in (b"\0\1\0\0", b"OTTO"), "Placeholder font is not a valid sfnt file")
    require((UI / "Typography/Fonts/LiberationSans - OFL.txt").is_file(), "Placeholder font license missing")
    for source in ("IceAuthoredHud.cs", "IceHudWidget.cs", "IceUIStyle.cs"):
        text = read(UI / "Runtime" / source)
        require("ExecuteAlways" not in text and "void OnValidate" not in text, f"Unexpected automatic Edit Mode visual mutation in {source}")
        require("anchoredPosition =" not in text and "sizeDelta =" not in text and "anchorMin =" not in text,
                f"Runtime script resets authored layout: {source}")
    print(f"PASS {checks} structural assertions. Unity import/render/persistence/native interaction checks remain separate.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, AttributeError) as error:
        print(f"FAIL {error}", file=sys.stderr)
        sys.exit(1)
