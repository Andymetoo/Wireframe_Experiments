"""Minimal guarded asset integration. Writes a candidate first; --apply verifies baseline hashes.
Does not rebake geometry, edit mesh/material assets, or change unrelated scene properties.
"""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import math
import re
import sys
sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
spec = importlib.util.spec_from_file_location('audit', HERE.parent/'OperatorTuning/AssetAudit.py')
a = importlib.util.module_from_spec(spec)
spec.loader.exec_module(a)
manifest = json.loads((HERE/'baseline-manifest.json').read_text(encoding='utf-8-sig'))
prefab_rel = 'Machine/AuroraR54.prefab'
scene_rel = 'Scenes/IceResurfacer_AuthoredMachine.unity'
source = (HERE/'Before'/prefab_rel).read_text()
docs = a.documents(HERE/'Before'/prefab_rel)
p = a.Prefab(HERE/'Before'/prefab_rel)
changes = {}
added = {}
next_id = 9000000000001000000
def alloc():
    global next_id
    next_id += 1
    assert next_id not in docs and next_id not in added
    return next_id
def replace(i, old, new):
    kind, text = changes.get(i, docs[i])
    assert text.count(old) == 1, (i, old)
    changes[i] = (kind, text.replace(old, new))
def append_child(parent, child):
    kind, text = changes.get(parent, docs.get(parent, added.get(parent)))
    if '  m_Children: []\n' in text:
        text = text.replace('  m_Children: []\n', f'  m_Children:\n  - {{fileID: {child}}}\n')
    else:
        text = text.replace('  m_Father:', f'  - {{fileID: {child}}}\n  m_Father:')
    (changes if parent in docs else added)[parent] = (kind, text)
def component(go, component_id):
    kind, text = changes.get(go, docs.get(go, added.get(go)))
    text = text.replace('  m_Layer:', f'  - component: {{fileID: {component_id}}}\n  m_Layer:')
    (changes if go in docs else added)[go] = (kind, text)
def vec(v):
    return '{' + ', '.join(f'{k}: {n:.9g}' for k,n in zip('xyzw',v)) + '}'
common = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
def node(name, parent, pos=(0,0,0), scale=(1,1,1), rotation=(0,0,0,1)):
    go, tr = alloc(), alloc()
    added[go] = (1, 'GameObject:\n'+common+f'  serializedVersion: 6\n  m_Component:\n  - component: {{fileID: {tr}}}\n  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n')
    added[tr] = (4, 'Transform:\n'+common+f'  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n  m_LocalRotation: {vec(rotation)}\n  m_LocalPosition: {vec(pos)}\n  m_LocalScale: {vec(scale)}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n')
    append_child(parent,tr)
    return go,tr
materials = {f.stem.replace('.mat',''): re.search(r'guid: (\w+)', f.read_text())[1] for f in (ROOT/'Machine/Materials').glob('*.meta')}
renderer_template = next(text for kind,text in docs.values() if kind == 23)
def mesh_node(name,parent,pos,size,material,mesh=10202):
    go,tr = node(name,parent,pos,size)
    mf,mr = alloc(),alloc()
    component(go,mf);component(go,mr)
    added[mf]=(33,'MeshFilter:\n'+common+f'  m_GameObject: {{fileID: {go}}}\n  m_Mesh: {{fileID: {mesh}, guid: 0000000000000000e000000000000000, type: 0}}\n')
    text=re.sub(r'm_GameObject: \{fileID: \d+\}',f'm_GameObject: {{fileID: {go}}}',renderer_template)
    text=re.sub(r'  m_Materials:\n(?:  - .*\n)+',f'  m_Materials:\n  - {{fileID: 2100000, guid: {materials[material]}, type: 2}}\n',text)
    added[mr]=(23,text)
    return go,tr
def mono(go,guid,fields=''):
    mid=alloc();component(go,mid)
    added[mid]=(114,'MonoBehaviour:\n'+common+f'  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n'+fields)
    return mid

# Compact original bracket: inner attachment remains fixed at machine X=-.632 m.
replace(5722889660757518116,'m_LocalPosition: {x: -1.0009999, y: 0.53, z: 2.444}','m_LocalPosition: {x: -0.827, y: 0.53, z: 2.444}')
replace(5818297522479415069,'x: 0.18449986,','x: 0.09749997,')
replace(5818297522479415069,'y: 0.18450001,','y: 0.097500004,')
replace(8650661190179171924,'  pivotHeight:', '  useAuthoredEndpoints: 0\n  complianceTravel: 0.12\n  springReturn: 12\n  springDamping: 1\n  pivotHeight:')

eye=p.parts['CameraMount']; eye_go=a.ref(p.transforms[eye],'m_GameObject')
replace(eye,'m_LocalPosition: {x: -0.21, y: 0.67, z: 0.09}','m_LocalPosition: {x: 0, y: 0.73, z: 0.14}')
replace(eye_go,'m_Name: Seated eyes / attached to driver seat','m_Name: Driver Eye Anchor')
# E camera: right of seat and higher, aimed at the valve/handwheel region.
yaw,pitch=map(math.radians,(69,44))
rotation=(math.sin(pitch/2)*math.cos(yaw/2),math.cos(pitch/2)*math.sin(yaw/2),-math.sin(pitch/2)*math.sin(yaw/2),math.cos(pitch/2)*math.cos(yaw/2))
_,controls_eye=node('Controls View Anchor',p.parts['Seat'],(.18,.86,-.05),rotation=rotation)

# Extend only the column mounting foot down to the floor; retain its top and the column pose.
replace(542850353373282033,'y: -0.52000004,','y: -0.53175004,')
replace(542850353373282033,'y: 0.0375,','y: 0.04925,')

station=p.parts['OperatorStation']; station_go=a.ref(p.transforms[station],'m_GameObject')
unbatched='7f8b960dad8bd284680abfebe2df2ef7'
# Small support under the three independently movable mounts, connected to the column.
rail_go,rail=node('Cockpit switch support / left of steering wheel',station,(-.29,1.35,.53))
mesh_node('Switch support rail',rail,(0,0,0),(.10,.018,.36),'Aurora midnight blue')
mesh_node('Switch rail column bracket',rail,(.15,-.022,0),(.30,.026,.045),'Brushed stainless steel')
moving=[]
for label,z,mat,horn in [('Board brush switch',.41,'Wash inlet - teal',False),('Horn button',.53,'Amber safety enamel',True),('Wash pickup pump switch',.65,'Flood water inlet - blue',False)]:
    go,mount=node(label+' / movable assembly',station,(-.29,1.368,z))
    mono(go,unbatched)
    mesh_node('Switch base',mount,(0,0,0),(.085,.018,.095),'Instrument graphite')
    _,pivot=node(label+' moving part',mount,(0,.014,0))
    if horn: mesh_node('Horn button cap',pivot,(0,.012,0),(.055,.011,.055),mat,10206)
    else: mesh_node('Cockpit switch grip',pivot,(0,.035,0),(.034,.055,.028),mat)
    moving.append(pivot)
mono(station_go,'4f691cc03f244a0f91bcbe752839ab40',f'  driverEyeAnchor: {{fileID: {eye}}}\n  controlsViewAnchor: {{fileID: {controls_eye}}}\n  controlsEnterSeconds: 0.24\n  controlsReturnSeconds: 0.24\n  boardBrushSwitch: {{fileID: {moving[0]}}}\n  hornButton: {{fileID: {moving[1]}}}\n  washPumpSwitch: {{fileID: {moving[2]}}}\n  switchOnDegrees: 24\n  hornTravel: 0.006\n')

for i,(kind,text) in changes.items():
    pattern=r'(^--- !u!'+str(kind)+' &'+str(i)+r'[^\n]*\n).*?(?=^--- !u!|\Z)'
    source,count=re.subn(pattern,lambda m:m[1]+text,source,flags=re.M|re.S)
    assert count==1,i
source+=''.join(f'--- !u!{kind} &{i}\n{text}' for i,(kind,text) in added.items())
scene=(HERE/'Before'/scene_rel).read_text()
assert scene.count('    GuideRollerX: -1.0009999\n')==1
scene=scene.replace('    GuideRollerX: -1.0009999\n','    GuideRollerX: -0.827\n')
outputs={prefab_rel:source,scene_rel:scene}
for rel,text in outputs.items():
    dest=HERE/'Candidate'/rel;dest.parent.mkdir(parents=True,exist_ok=True)
    # Preserve original CRLF serialization on Windows.
    dest.write_bytes(text.replace('\n','\r\n').encode('utf-8'))
(HERE/'asset-delta.json').write_text(json.dumps({'changed_original_documents':list(changes),'new_documents':list(added),'camera_anchor':controls_eye,'moving_controls':moving},indent=2))
if argparse.ArgumentParser().parse_known_args()[1]==['--apply']:
    applied_path=HERE/'applied-manifest.json'
    expected=json.loads(applied_path.read_text(encoding='utf-8-sig')) if applied_path.exists() else manifest
    for rel in outputs:
        actual=hashlib.sha256((ROOT/rel).read_bytes()).hexdigest().upper()
        assert actual==expected[rel],f'Refusing to overwrite a file changed since verified baseline/application: {rel}'
    for rel in outputs:(ROOT/rel).write_bytes((HERE/'Candidate'/rel).read_bytes())
    applied_path.write_text(json.dumps({rel:hashlib.sha256((ROOT/rel).read_bytes()).hexdigest().upper() for rel in outputs},indent=2))
    print('Applied guarded minimal prefab and GuideRollerX scene changes.')
else: print('Candidate prepared; original assets untouched. Run --apply after review.')
print('Changed existing prefab documents:',len(changes),'Added documents:',len(added))
