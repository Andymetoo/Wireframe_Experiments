"""Read-only machine-space sightline audit; not a rendered Unity validation."""
from pathlib import Path
import importlib.util
import json
import struct
import sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
spec = importlib.util.spec_from_file_location("asset_audit", HERE.parent / "OperatorTuning/AssetAudit.py")
a = importlib.util.module_from_spec(spec)
spec.loader.exec_module(a)
p = a.Prefab(ROOT / "Machine/AuroraR54.prefab")
frame = p.parts["Machine"]
sub = lambda x, y: [u-v for u, v in zip(x, y)]
dot = lambda x, y: sum(u*v for u, v in zip(x, y))

def cross(u, v):
    return [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]

def hit(origin, target, tri):
    direction = sub(target, origin)
    edge1, edge2 = sub(tri[1], tri[0]), sub(tri[2], tri[0])
    h = cross(direction, edge2)
    det = dot(edge1, h)
    if abs(det) < 1e-10:
        return False
    s = sub(origin, tri[0])
    u = dot(s, h)/det
    if u < 0 or u > 1:
        return False
    q = cross(s, edge1)
    v = dot(direction, q)/det
    t = dot(edge2, q)/det
    return v >= 0 and u+v <= 1 and .0001 < t < .9999

def active(tr):
    while tr:
        if a.scalar(p.docs[a.ref(p.transforms[tr], "m_GameObject")][1], "m_IsActive") == "0":
            return False
        tr = a.ref(p.transforms[tr], "m_Father")
    return True

enabled = {a.ref(t, "m_GameObject") for kind, t in p.docs.values() if kind == 23 and a.scalar(t, "m_Enabled") == "1"}
triangles = []
for kind, text in p.docs.values():
    if kind != 33 or a.ref(text, "m_GameObject") not in enabled:
        continue
    tr = p.by_game[a.ref(text, "m_GameObject")]
    mesh = a.ref(text, "m_Mesh")
    if not mesh or not active(tr):
        continue
    matrix = p.relative(tr, frame)
    vertices = [a.point(matrix, v) for v in p.vertices(mesh)]
    if mesh in p.meshes:
        data = bytes.fromhex(a.scalar(p.meshes[mesh][1], "m_IndexBuffer"))
        code = "H" if a.scalar(p.meshes[mesh][1], "m_IndexFormat") == "0" else "I"
        indices = [v[0] for v in struct.iter_unpack("<"+code, data)]
    else:
        # Conservative built-in primitive bounds. False positives can only
        # hide a clear line, never falsely certify a blocked one as clear.
        indices = [0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3]
    for i in range(0, len(indices), 3):
        triangles.append((p.name(tr), tuple(vertices[k] for k in indices[i:i+3])))

outlet = a.point(p.relative(p.parts["TankDischarge"], frame))
outlet[2] += .055
opening = a.point(p.relative(p.parts["TankInspection"], frame))
velocity = [(opening[0]-outlet[0])/.30, 1.05, (opening[2]+.16-outlet[2])/.30]
eye = a.point(p.relative(p.parts["CameraMount"], frame))
eye[1] += .48+.06
eye[2] += .10
records = []
for time in [.16,.20,.24,.28,.30,.32,.36,.40]:
    target = [outlet[k]+velocity[k]*time-(4.905*time*time if k == 1 else 0) for k in range(3)]
    blockers = sorted({name for name, tri in triangles if hit(eye, target, tri)})
    records.append(dict(time_s=time, stream_point_m=target, blockers=blockers, clear=not blockers))
assert sum(r["clear"] for r in records) >= 3, records
result = dict(proof="Read-only static authored-mesh intersection, not rendered proof; built-in primitive AABBs are conservative. Empty/lowered bin; dynamic snow stock not modelled.",
              triangles=len(triangles), standing_eye_m=eye, outlet_m=outlet, opening_m=opening, velocity_mps=velocity, samples=records)
(HERE / "inspection-audit.json").write_text(json.dumps(result, indent=2))
print(json.dumps(result, indent=2))
