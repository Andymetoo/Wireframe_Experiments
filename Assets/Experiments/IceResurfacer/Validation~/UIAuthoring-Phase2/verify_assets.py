"""Read-only Phase 2 prefab structure checks; Unity behavior is verified separately.

Run with Python 3 from any directory. Uses the shared standard-library YAML
reference reader from the Phase 1 validation folder; does not modify assets.
"""
from __future__ import annotations

import importlib.util
from pathlib import Path
import re
import sys

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("phase1_assets", HERE.parent / "UIAuthoring-Phase1/verify_assets.py")
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)
require, field, local = v.require, v.field, v.local


def main() -> None:
    folder = v.UI / "Prefabs/Machinery"
    paths = sorted(folder.rglob("*.prefab"))
    expected = {
        "Aurora Machinery Console", "Aurora Action Button", "Aurora Maintained Toggle",
        "Aurora Momentary Button", "Aurora Hydraulic Lever", "Aurora Quarter Turn Valve",
        "Aurora Blade Wheel", "Aurora Bound Slider", "Aurora Collapsible Panel",
    }
    require({p.stem for p in paths} == expected and len(paths) == 9, "Expected console and eight reusable templates")
    lookup = v.resolve_guids()
    v.verify_references(paths, lookup)
    control_guid = v.asset_guid(v.UI / "Runtime/IceConsoleControl.cs")
    owner_guid = v.asset_guid(v.UI / "Runtime/IceMachineryConsole.cs")
    style_guid = v.asset_guid(v.UI / "Runtime/IceUIStyle.cs")
    custom_count = 0
    for path in paths:
        require(v.asset_guid(path) in lookup, f"Missing prefab metadata: {path.name}")
        objects = v.docs(v.read(path))
        scripts = {ident: v.REF.fullmatch(field(body, "m_Script"))[2]
                   for ident, (kind, body) in objects.items() if kind == 114}
        controls = {ident: body for ident, (_, body) in objects.items() if scripts.get(ident) == control_guid}
        styles = {local(body, "m_GameObject"): body for ident, (_, body) in objects.items() if scripts.get(ident) == style_guid}
        for ident, control in controls.items():
            kind = int(field(control, "kind"))
            require(local(control, "interactionArea") in objects, f"Control interaction rectangle: {path.name}/{kind}")
            if kind in (3, 5, 6, 7, 8) and local(control, "slider") == 0:
                selectable_id = local(control, "selectable")
                require(selectable_id in objects, f"Missing custom Selectable: {path.name}/{kind}")
                selectable = objects[selectable_id][1]
                target = local(selectable, "m_TargetGraphic")
                require(target in objects, f"Missing selectable graphic: {path.name}/{kind}")
                require(field(selectable, "m_Transition") == "1", f"Custom ColorTint state enabled: {path.name}/{kind}")
                style = styles[local(control, "m_GameObject")]
                require(local(style, "selectable") == selectable_id and field(style, "applyButtonStates") == "1",
                        f"Custom state uses shared theme: {path.name}/{kind}")
                if kind == 8:
                    require(target == local(control, "indicator"), f"Blade state target must be visible hub: {path.name}")
                if kind in (6, 7, 8):
                    rotor = objects[local(control, "movingGraphic")][1]
                    require(field(rotor, "m_LocalRotation") == "{x: 0, y: 0, z: 0, w: 1}",
                            f"Rotor has neutral baseline; runtime angle applied once: {path.name}/{kind}")
                    require(v.vector(rotor, "m_AnchorMin") == (.5, .5) and v.vector(rotor, "m_AnchorMax") == (.5, .5) and
                            v.vector(rotor, "m_AnchoredPosition") == (0, 0),
                            f"Rotor follows resized interaction center: {path.name}/{kind}")
                    value_rect = next(body for native_kind, body in objects.values() if native_kind == 224 and
                                      local(body, "m_GameObject") == local(objects[local(control, "value")][1], "m_GameObject"))
                    require(v.vector(value_rect, "m_AnchorMin") == (0, 0) and v.vector(value_rect, "m_AnchorMax") == (1, 0),
                            f"Readout follows resized control bottom: {path.name}/{kind}")
                custom_count += 1
        # Every TMP label references an imported font rather than a missing default.
        for ident, (_, body) in objects.items():
            if "  m_fontAsset:" in body:
                ref = v.REF.fullmatch(field(body, "m_fontAsset"))
                require(int(ref[1]) != 0 and ref[2] in lookup, f"TMP font assigned: {path.name}/{ident}")
                require(field(body, "m_RaycastTarget") == "0", f"Decoration text cannot steal gestures: {path.name}/{ident}")
        if path.stem != "Aurora Machinery Console":
            continue
        owners = [(ident, body) for ident, (_, body) in objects.items() if scripts.get(ident) == owner_guid]
        require(len(owners) == 1, "Console has exactly one controller")
        owner_id, owner = owners[0]
        kinds = {int(field(body, "kind")): (ident, body) for ident, body in controls.items()}
        require(set(kinds) == set(range(18)) and len(controls) == 18, "All 18 unique live controls are serialized")
        bound_list = re.search(r"^  controls:\n((?:  - .*\n)+)", owner, re.M)
        require(bound_list is not None, "Controller controls array exists")
        require({int(m[1]) for m in v.REF.finditer(bound_list[1])} == set(controls), "Controller array matches every control")
        for control in controls.values():
            require(local(control, "owner") == owner_id, "Each control has explicit owner")
        for key in ("game", "canvas", "raycaster"):
            require(local(owner, key) == 0, f"Reusable console receives scene {key} reference at installation")
        for key, active in (("compactGroup", "1"), ("expandedGroup", "0"), ("confirmationGroup", "0")):
            group_id = local(owner, key)
            require(objects[group_id][0] == 225, f"Serialized visibility group: {key}")
            group = objects[group_id][1]
            require(field(group, "m_Alpha") == active and field(group, "m_BlocksRaycasts") == active,
                    f"Default visibility and raycast state: {key}")
        root = local(owner, "m_GameObject")
        require(not any(local(body, "m_GameObject") == root and "  m_RaycastTarget:" in body
                        for _, body in objects.values() if "  m_GameObject:" in body),
                "Full-screen root has no invisible Graphic raycast blocker")
        require(field(owner, "confirmNewSession") == "1" and field(owner, "confirmFinish") == "0", "Terminal-action confirmation policy")
        for kind, expected_text in {2: "ON", 3: "45% UP", 4: "OFF", 5: "0% UP", 6: "45%", 7: "30%", 8: "0.10 mm"}.items():
            body = objects[local(kinds[kind][1], "value")][1]
            require(field(body, "m_text").strip('"') == expected_text, f"Representative preview for kind {kind}")
        for kind in (9, 10):
            indicator = objects[local(kinds[kind][1], "indicator")][1]
            require(field(indicator, "m_FillAmount") == "1", f"Maintained ON preview for kind {kind}")
        for key in ("schematicBody", "schematicBlade", "schematicSetMarker"):
            require(local(kinds[15][1], key) in objects, f"Live blade schematic reference: {key}")
    require(custom_count == 8, "Five console and three template custom state targets")
    for name in ("IceMachineryConsole.cs", "IceConsoleControl.cs"):
        text = v.read(v.UI / "Runtime" / name)
        require("ExecuteAlways" not in text and "void OnValidate" not in text, f"No automatic Edit Mode visual mutation: {name}")
        require("anchoredPosition =" not in text and "sizeDelta =" not in text, f"Runtime preserves authored control layout: {name}")
    print(f"PASS {v.checks} serialized assertions across 9 Phase 2 prefabs. Unity rendering, interactions and persistence are separate evidence.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, AttributeError, TypeError) as error:
        print(f"FAIL {error}", file=sys.stderr)
        sys.exit(1)
