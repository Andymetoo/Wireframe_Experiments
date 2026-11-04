"""Read-only Unity text-asset audit. Standard library; never imports or saves a Unity asset."""
from pathlib import Path
import hashlib
import itertools
import json
import math
import re
import struct

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent

def documents(path):
    text = path.read_text(encoding="utf-8-sig")
    return {int(m[2]): (int(m[1]), m[3]) for m in re.finditer(
        r"^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S)}

def scalar(text, key):
    return re.search(r"^\s*" + re.escape(key) + r":\s*([^\n]+)", text, re.M)[1]

def ref(text, key):
    return int(re.search(r"fileID:\s*(-?\d+)", scalar(text, key))[1])

def vector(text, key):
    return [float(v) for v in re.findall(r"[xyzw]:\s*([-+\d.eE]+)", scalar(text, key))]

def identity():
    return [[float(i == j) for j in range(4)] for i in range(4)]

def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

def inverse(m):
    rows = [m[i][:] + identity()[i] for i in range(4)]
    for k in range(4):
        pivot = max(range(k, 4), key=lambda i: abs(rows[i][k]))
        rows[k], rows[pivot] = rows[pivot], rows[k]
        d = rows[k][k]
        rows[k] = [v / d for v in rows[k]]
        for i in range(4):
            if i == k:
                continue
            f = rows[i][k]
            rows[i] = [rows[i][j] - f * rows[k][j] for j in range(8)]
    return [r[4:] for r in rows]

def point(m, p=(0, 0, 0)):
    return [sum(m[i][k] * (*p, 1)[k] for k in range(4)) for i in range(3)]

def local(text):
    p, q, s = vector(text, "m_LocalPosition"), vector(text, "m_LocalRotation"), vector(text, "m_LocalScale")
    x, y, z, w = [v / math.sqrt(sum(t*t for t in q)) for v in q]
    r = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
         [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
         [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]
    m = identity()
    for i in range(3):
        for j in range(3):
            m[i][j] = r[i][j]*s[j]
        m[i][3] = p[i]
    return m

class Prefab:
    def __init__(self, path):
        self.docs = documents(path)
        self.transforms = {i: t for i, (kind, t) in self.docs.items() if kind == 4}
        self.by_game = {ref(t, "m_GameObject"): i for i, t in self.transforms.items()}
        self.world_cache = {}
        self.parts = {}
        for kind, t in self.docs.values():
            if kind == 114:
                for name, identifier in re.findall(r"- field: (\w+)\n\s+target: \{fileID: (-?\d+)\}", t):
                    self.parts[name] = int(identifier)
        self.meshes = documents(path.with_name("AuroraR54.Meshes.asset")) if path.with_name("AuroraR54.Meshes.asset").exists() else {}

    def world(self, i):
        if i == 0:
            return identity()
        if i not in self.world_cache:
            t = self.transforms[i]
            self.world_cache[i] = mul(self.world(ref(t, "m_Father")), local(t))
        return self.world_cache[i]

    def relative(self, i, frame):
        return mul(inverse(self.world(frame)), self.world(i))

    def name(self, i):
        return scalar(self.docs[ref(self.transforms[i], "m_GameObject")][1], "m_Name")

    def descendants(self, i, parent):
        while i:
            if i == parent:
                return True
            i = ref(self.transforms[i], "m_Father")
        return False

    def vertices(self, mesh):
        if mesh in self.meshes:
            t = self.meshes[mesh][1]
            count = int(scalar(t, "m_VertexCount"))
            data = bytes.fromhex(scalar(t, "_typelessdata"))
            stride = len(data)//count
            return [struct.unpack_from("<3f", data, i*stride) for i in range(count)]
        # Built-in box/cylinder/sphere AABBs; selected cylinders are upright.
        assert mesh in (10202, 10206, 10207, 10208, 10209, 10210), mesh
        extents = (.5, 1 if mesh in (10206, 10208) else .5, .5)
        return list(itertools.product(*[(-e, e) for e in extents]))

    def bounds(self, node, frame, exclude=()):
        vertices = []
        for kind, t in self.docs.values():
            if kind != 33:
                continue
            tr = self.by_game[ref(t, "m_GameObject")]
            if self.descendants(tr, node) and not any(self.descendants(tr, e) for e in exclude):
                if ref(t, "m_Mesh") == 0:
                    continue  # Authored dynamic surfaces receive their mesh at runtime.
                m = self.relative(tr, frame)
                vertices += [point(m, p) for p in self.vertices(ref(t, "m_Mesh"))]
        assert vertices, self.name(node)
        return {"min": [min(v[k] for v in vertices) for k in range(3)],
                "max": [max(v[k] for v in vertices) for k in range(3)]}

def numeric_block(text, label, indent):
    match = re.search(r"^" + " "*indent + label + r":\n(.*?)(?=^ {0," + str(indent) + r"}\S|\Z)", text, re.M | re.S)
    result = {}
    for key, number in re.findall(r"^\s+(\w+): ([-+\d.eE]+)\s*$", match[1], re.M):
        result[key] = float(number)
    outline = re.search(r"^\s+CollisionOutline:\n((?:\s+- [-+\d.eE]+\n)+)", match[1], re.M)
    if outline:
        result["CollisionOutline"] = [float(n) for n in re.findall(r"- ([-+\d.eE]+)", outline[1])]
    return result

def main():
    prefab = Prefab(ROOT/"Machine/AuroraR54.prefab")
    machine, seat, eye = [prefab.parts[n] for n in ("Machine", "Seat", "CameraMount")]
    result = {"units": "metres, machine space unless specified", "camera": {
        "anchor": prefab.name(eye), "parent": prefab.name(ref(prefab.transforms[eye], "m_Father")),
        "machine": point(prefab.relative(eye, machine)), "seat_local": point(prefab.relative(eye, seat)),
        "seat_machine": point(prefab.relative(seat, machine))}, "parts": {}}
    for identifier in prefab.transforms:
        name = prefab.name(identifier)
        if name in ("Operator cushion", "Operator seat back", "Exposed conditioner side plate", "Guide roller bracket"):
            result["parts"][name+" "+str(identifier)] = {"machine": prefab.bounds(identifier, machine), "seat_local": prefab.bounds(identifier, seat)}
    roller = prefab.parts["GuideRoller"]
    result["roller"] = {"centre": point(prefab.relative(roller, machine)), "bounds": prefab.bounds(roller, machine)}
    roller_assembly = ref(prefab.transforms[roller], "m_Father")
    result["body_bounds_excluding_brush_roller_conditioner"] = prefab.bounds(machine, machine, (roller_assembly, prefab.parts["BrushArm"], prefab.parts["Conditioner"]))
    result["conditioner_assembly_bounds_authored_pose"] = prefab.bounds(prefab.parts["Conditioner"], machine)
    result["brush"] = {"arm": point(prefab.relative(prefab.parts["BrushArm"], machine)), "head": point(prefab.relative(prefab.parts["Brush"], machine))}
    scene = (ROOT/"Scenes/IceResurfacer_AuthoredMachine.unity").read_text()
    calibration = numeric_block(scene, "calibration", 2)
    tuning = numeric_block(scene, "simulation", 2)
    scene_data = {"calibration": calibration, "tuning": tuning}
    outline = list(zip(calibration["CollisionOutline"][::2], calibration["CollisionOutline"][1::2]))
    offset = calibration["HitchBehindAxle"] + calibration["DrawbarLength"]
    conditioner = list(itertools.product((-calibration["ConditionerCollisionHalfWidth"], calibration["ConditionerCollisionHalfWidth"]),
        (-offset-calibration["ConditionerCollisionRear"], -offset+calibration["ConditionerCollisionFront"])))
    result["left_board_contact_envelope"] = []
    for degrees in (-5, -1, 0, 1, 5):
        angle = math.radians(degrees)
        reach = lambda points: -min(x*math.cos(angle)+z*math.sin(angle) for x,z in points)
        roller_reach = -(calibration["GuideRollerX"]*math.cos(angle)+calibration["GuideRollerZ"]*math.sin(angle))+calibration["GuideRollerRadius"]
        result["left_board_contact_envelope"].append({"heading_degrees": degrees, "body_reach_m": reach(outline), "conditioner_reach_m": reach(conditioner), "roller_reach_m": roller_reach})
    # Every original scene document is unchanged except the root component list.
    original = documents(ROOT/"Backups~/2026-11-04 Operator tuning first pass/Scenes/IceResurfacer_AuthoredMachine.unity")
    current = documents(ROOT/"Scenes/IceResurfacer_AuthoredMachine.unity")
    assert set(current) - set(original) == {1003}
    for identifier, (kind, content) in original.items():
        actual = current[identifier][1]
        if identifier == 1000:
            actual = actual.replace("  - component: {fileID: 1003}\n", "")
        assert actual == content, f"Unexpected authored scene edit: {identifier}"
    result["scene_preservation"] = "PASS: only added component 1003 and root reference; all original documents, calibration and prefab overrides unchanged"
    (HERE/"scene-config.json").write_text(json.dumps(scene_data, indent=2))
    result["authored_brush_working_centre"] = [calibration["BrushPivotX"]-calibration["BrushArmLength"], calibration["BrushPivotZ"]]
    result["authored_brush_outer_reach"] = -result["authored_brush_working_centre"][0]+calibration["BrushRadius"]
    # The pre-pass backup preserves the operator's pre-extension roller position.
    old_path = ROOT/"Backups~/2026-11-03 082350 Before controls brush roller package/Machine/AuroraR54.prefab"
    if old_path.exists():
        old = Prefab(old_path)
        result["pre_extension_roller_centre"] = point(old.relative(old.parts["GuideRoller"], old.parts["Machine"]))
    result["hashes"] = {name: hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in ("Machine/AuroraR54.prefab", "Machine/AuroraR54.Meshes.asset", "Scenes/IceResurfacer.unity")}
    (HERE/"asset-audit.json").write_text(json.dumps(result, indent=2))
    print(json.dumps(result, indent=2))

if __name__ == "__main__":
    main()
