"""Offline sightline search, preserves all assets."""
import CameraGeometry as g
import math
import re
import json
a,p,frame=g.a,g.p,g.frame
seat=p.parts['Seat'];blade=p.parts['BladeWheel'];conditioner=p.parts['Conditioner'];rest=p.transforms[conditioner]
sets=[]
for lift in [0,.5,1]:
    xyz=a.vector(rest,'m_LocalPosition');xyz[1]=lift*.34
    p.transforms[conditioner]=re.sub(r'm_LocalPosition: \{[^}]+\}',f'm_LocalPosition: {{x: {xyz[0]}, y: {xyz[1]}, z: {xyz[2]}}}',rest);p.world_cache.clear()
    geometry=[(t,tri) for t,tri in g.triangles() if any(p.descendants(t,p.parts[k]) for k in ['FloodLever','WashValve','Seat'])]
    grips=[]
    for key in ['ConditionerLever','HorizontalAugerLever','VerticalAugerLever','DumpLever','FloodLever','WashValve','SnowBreakerPlunger']:
        part=p.parts[key];grip=next((t for t in p.transforms if p.descendants(t,part) and p.name(t) in ['Black phenolic hydraulic grip','Valve grip','Snow breaker T handle']),part)
        grips.append(a.point(p.relative(grip,frame)))
    rim=[a.point(p.relative(blade,frame),(.13*math.cos(n*math.pi/4),.012,.13*math.sin(n*math.pi/4))) for n in range(8)]
    sets.append((lift,geometry,grips,rim))
print('geometry loaded',sum(len(s[1]) for s in sets),flush=True)
choices=[]
for x in [.12,.18,.24,.30]:
 for y in [.86,.92,.98]:
  for z in [-.10,-.05,0,.05,.10,.15]:
   eye=a.point(p.relative(seat,frame),(x,y,z));visible=[]
   for lift,geometry,grips,rim in sets:
    count=sum(not any(g.hit(eye,pt,tri) for t,tri in geometry) for pt in rim);visible.append(count)
    if count<6:break
   if len(visible)<3 or min(visible)<6:continue
   points=[pt for _,_,grips,rim in sets for pt in grips+rim]
   best=None
   for yaw in range(65,131,2):
    cy,sy=math.cos(math.radians(yaw)),math.sin(math.radians(yaw))
    for pitch in range(28,66):
     cp,sp=math.cos(math.radians(pitch)),math.sin(math.radians(pitch));uv=[]
     for pt in points:
      dx,dy,dz=[pt[i]-eye[i] for i in range(3)];xx=cy*dx-sy*dz;zz=sy*dx+cy*dz;yy=cp*dy+sp*zz;zz=-sp*dy+cp*zz
      if zz<=0:break
      uv.append((.5+xx/zz/math.tan(math.radians(30.5))/(16/9)/2,.5+yy/zz/math.tan(math.radians(30.5))/2))
     if len(uv)!=len(points):continue
     margin=min(min(u,v,1-u,1-v) for u,v in uv)
     if margin>=.02 and (best is None or margin>best[0]):best=(margin,yaw,pitch)
   if best:
    cost=abs(x-.18)+abs(y-.86)+abs(z-.15)
    choices.append(dict(cost=cost,position=[x,y,z],visible=visible,framing=best))
choices.sort(key=lambda c:c['cost']);(g.HERE/'controls-view-options.json').write_text(json.dumps(choices,indent=2))
print(json.dumps(choices[:10],indent=2))
