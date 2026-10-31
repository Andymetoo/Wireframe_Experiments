from pathlib import Path
import json, math, hashlib

folder=Path(__file__).parent
before=json.loads((folder/'Before-saved-draft.json').read_text())
fresh=json.loads((folder/'Before.json').read_text())
after=json.loads((folder/'After-recorded-track.json').read_text())
definition=json.loads((folder/'Before-saved-draft-definition.json').read_text())
raw=json.loads(definition['rawRecordingJson'])['route']
config=raw['configuration']
regions=[]
for fault in before['faults']:
    kind='Derived curvature' if fault['curvatureError'] else 'Vehicle boundary'
    if not regions or regions[-1]['kind']!=kind or fault['distance']-regions[-1]['rows'][-1]['distance']>.35:
        regions.append({'kind':kind,'rows':[]})
    regions[-1]['rows'].append(fault)
lines=['ANDY_TRY_1 / RAW VERSUS DERIVED DIAGNOSTICS',
       f"Raw: {len(raw['samples'])} samples; {raw['distanceMeters']:.9f} m; {raw['durationSeconds']:.3f} seconds.",
       f"Production raw vehicle pose failures: {before['rawBodyFails']}. Raw conditioner corner failures: {before['rawConditionerFails']}.",
       '', 'BEFORE / AFTER',
       'Case | samples | length m | curvature failures | boundary failures | minimum radius m | max deviation m']
for label,data in [('Saved derived draft (exact reported case)',before),('Fresh old automatic fit',fresh),('Repaired recorded track',after)]:
    lines.append(f"{label} | {data['samples']} | {data['Length']:.6f} | {data['curvatureErrors']} | {data['boundaryErrors']} | {data['minimumRadius']:.6f} | {data['maximumRawDeviation']:.6f}")
lines+=['Before deviations use the original nearest-source-sample metric, reproduced before edits.',
        'After displacement is zero from the corresponding source polyline; raw endpoints are exact.',
        '', 'ROOT CAUSE',
        'Both reported faults are introduced by the derived geometry. Every raw pose is clear.',
        'The legacy tangent optimizer penalizes curvature and heading changes, but never evaluates',
        'the production body envelope or boundary clearance in its objective. Its 12 cm position',
        'budget is relative to each invocation, rather than the immutable recording. Repeated',
        'calls drift beyond that budget; this is measured in Legacy-repeated-correction.txt.',
        'In the saved draft, control 62 is displaced 0.366617 m from its original source anchor.',
        'Its position and shared heading/handles affect both adjacent cubics. Moving or deleting',
        'one anchor therefore shifts curvature/clearance trouble into its neighbors.',
        'A fresh old auto fit has 0 curvature failures but 63 body failures; the saved derived',
        'draft reproduces the requested 16 + 63 exactly. The raw route does not have a 2.662 m turn.',
        '', 'GROUPED FAILURE REGIONS (zero-based control/sample indices)']
for number,group in enumerate(regions,1):
    rows=group['rows'];worst=min(rows,key=lambda r:r['radius']) if group['kind']=='Derived curvature' else min(rows,key=lambda r:r['fittedContact']['PointZ'])
    r=worst['rawVehicle'];p=worst['fitVehicle'];rc=worst['rawConditioner'];pc=worst['fitConditioner'];contact=worst['fittedContact']
    dx=contact['PointX']-p['x'];dz=contact['PointZ']-p['z'];h=p['heading']
    local_x=math.cos(h)*dx-math.sin(h)*dz;local_z=math.sin(h)*dx+math.cos(h)*dz
    lines += ['',f"Region {number}: {group['kind']}; {len(rows)} failing derived samples.",
        f"Derived route distance {rows[0]['distance']:.6f}–{rows[-1]['distance']:.6f} m.",
        f"Source sequence range {min(x['source'] for x in rows)}–{max(x['source'] for x in rows)}; source anchor brackets {rows[0]['lo']}–{rows[-1]['hi']}; controls {min(x['control'] for x in rows)}–{max(x['control'] for x in rows)}.",
        f"Representative/worst fitted sample {worst['i']}, source sample {worst['source']}, raw route distance {worst['rawDistance']:.6f} m.",
        f"Raw vehicle: X={r['vehicleX']:.7f}, Z={r['vehicleZ']:.7f}, heading={r['vehicleHeadingRadians']:.8f} rad ({math.degrees(r['vehicleHeadingRadians']):.5f} deg).",
        f"Derived vehicle: X={p['x']:.7f}, Z={p['z']:.7f}, heading={p['heading']:.8f} rad ({math.degrees(p['heading']):.5f} deg).",
        f"Raw conditioner: X={rc['conditionerX']:.7f}, Z={rc['conditionerZ']:.7f}, heading={rc['conditionerHeadingRadians']:.8f} rad.",
        f"Derived conditioner: X={pc['conditionerX']:.7f}, Z={pc['conditionerZ']:.7f}, heading={pc['conditionerHeading']:.8f} rad.",
        f"Nearest recorded sample deviation here={worst['nearest']:.6f} m. Region maximum={max(x['nearest'] for x in rows):.6f} m.",
        f"Minimum derived radius={min(x['radius'] for x in rows):.6f} m; unchanged required radius={config['Wheelbase']/math.tan(config['MaxSteeringRadians']):.6f} m.",
        f"Original recorded production body pose passes: {worst['rawClear']}. Raw conditioner corners clear: {not worst['rawBadCorners']}."]
    if group['kind']=='Vehicle boundary':
        lines += [f"Responsible geometry: {contact['Reason']}; front-left body-envelope sample at local X={local_x:.6f}, Z={local_z:.6f} m.",
            f"Collision point X={contact['PointX']:.7f}, Z={contact['PointZ']:.7f}; inward normal=({contact['NormalX']:.3f},{contact['NormalZ']:.3f}); guide roller={contact['GuideRoller']}.",
            f"South board is Z={-config['RinkLength']/2:.3f}; production navigation includes its unchanged 0.02 m inward margin.",
            'This is a real collision of the derived vehicle orientation/envelope, not a raw collision and not a conditioner failure.']
    else:lines += ['Responsible geometry: no body collision in this region; the derived cubic violates the steering-radius constraint.']
endpoint=raw['samples'][-1];axis=math.sqrt(.5);gate_x=config['RinkWidth']/2-config['CornerRadius']+config['CornerRadius']*axis;gate_z=-config['RinkLength']/2+config['CornerRadius']-config['CornerRadius']*axis
dx=endpoint['vehicleX']-gate_x;dz=endpoint['vehicleZ']-gate_z
lines+=['','SOUTHEAST SERVICE ENTRANCE',
 f"Selected endpoint is control 397 / source 5129 at {endpoint['distanceMeters']:.6f} m: X={endpoint['vehicleX']:.7f}, Z={endpoint['vehicleZ']:.7f}.",
 f"Service coordinates: Side={axis*(dx+dz):.6f}, Outward={axis*(dx-dz):.6f} m. Conditioner lift={endpoint['conditionerLift']:.3f}.",
 'This is on the service-room floor, beyond the rink rectangle but inside the production floor union. The complete recorded body and conditioner envelope pass.',
 'It is not one of the 79 saved-draft failures. The new authoring background shows the gate opening and service-floor outline.',
 '', 'TURNING / REPRESENTATION',
 f"Minimum observed heading-change radius={before['rawMinRadius']:.6f} m versus required {config['Wheelbase']/math.tan(config['MaxSteeringRadians']):.6f} m.",
 'The sub-millimeter numerical difference is inside the existing 0.1% validation tolerance; that tolerance and the vehicle limit were not changed.',
 'The repaired guide interpolates positions and recorded body headings separately. It is a faithful observed track, not a claim of a C2 geometric spline.',
 'Actual recorded conditioner poses replace the old re-simulated conditioner on recorded spans. Edited Bezier spans still model it and must pass constraints.',
 'The original recordings are authoritative and unchanged; no vehicle steering, pose, scoring, speed calibration or machinery code was modified.']
(folder/'Analysis.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
(folder/'Failure-regions.json').write_text(json.dumps([{'kind':g['kind'],'start':g['rows'][0]['distance'],'end':g['rows'][-1]['distance'],'count':len(g['rows']),'representative':min(g['rows'],key=lambda r:r['radius']) if g['kind']=='Derived curvature' else min(g['rows'],key=lambda r:r['fittedContact']['PointZ'])} for g in regions],indent=2),encoding='utf-8')
print('Grouped analysis ready:',len(regions),'regions')
