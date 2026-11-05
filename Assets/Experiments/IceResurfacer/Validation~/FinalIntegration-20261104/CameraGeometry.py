"""Static authored-mesh camera/picking audit. This is not a rendered or Input System test."""
from pathlib import Path
import importlib.util
import json
import math
import re
import struct
import sys
sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
spec = importlib.util.spec_from_file_location('asset_audit', HERE.parent/'OperatorTuning/AssetAudit.py')
a = importlib.util.module_from_spec(spec)
spec.loader.exec_module(a)
path = Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'Machine/AuroraR54.prefab'
p = a.Prefab(path)
p.meshes = a.documents(ROOT/'Machine/AuroraR54.Meshes.asset')
frame = p.parts['Machine']
anchor = next(t for t in p.transforms if p.name(t)=='Controls View Anchor')
sub = lambda x,y:[u-v for u,v in zip(x,y)]
dot = lambda x,y:sum(u*v for u,v in zip(x,y))
def cross(u,v):return [u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
def hit(origin,target,tri):
    direction=sub(target,origin);e1,e2=sub(tri[1],tri[0]),sub(tri[2],tri[0]);h=cross(direction,e2);det=dot(e1,h)
    if abs(det)<1e-10:return False
    s=sub(origin,tri[0]);u=dot(s,h)/det
    if not 0<=u<=1:return False
    q=cross(s,e1);v=dot(direction,q)/det;t=dot(e2,q)/det
    return v>=0 and u+v<=1 and .0001<t<.9999
def active(tr):
    while tr:
        if a.scalar(p.docs[a.ref(p.transforms[tr],'m_GameObject')][1],'m_IsActive')=='0':return False
        tr=a.ref(p.transforms[tr],'m_Father')
    return True
def triangles():
    enabled={a.ref(t,'m_GameObject') for kind,t in p.docs.values() if kind==23 and a.scalar(t,'m_Enabled')=='1'}
    result=[]
    for kind,t in p.docs.values():
        if kind!=33 or a.ref(t,'m_GameObject') not in enabled:continue
        tr=p.by_game[a.ref(t,'m_GameObject')];mesh=a.ref(t,'m_Mesh')
        if not mesh or not active(tr):continue
        matrix=p.relative(tr,frame)
        vertices=[a.point(matrix,v) for v in p.vertices(mesh)]
        if mesh in p.meshes:
            data=bytes.fromhex(a.scalar(p.meshes[mesh][1],'m_IndexBuffer'));code='H' if a.scalar(p.meshes[mesh][1],'m_IndexFormat')=='0' else 'I'
            indices=[v[0] for v in struct.iter_unpack('<'+code,data)]
        else:indices=[0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3]
        result.extend((tr,tuple(vertices[k] for k in indices[i:i+3])) for i in range(0,len(indices),3))
    return result
def viewport(point):
    x,y,z=a.point(a.inverse(p.relative(anchor,frame)),point)
    return [.5+x/z/math.tan(math.radians(30.5))/(16/9)/2,.5+y/z/math.tan(math.radians(30.5))/2,z]

print("loaded", flush=True)
