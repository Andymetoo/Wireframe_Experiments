"""Compare integration against the verified local start snapshot, not Git HEAD."""
from pathlib import Path
import hashlib
import json
import re
import os
HERE=Path(__file__).resolve().parent
ROOT=HERE.parent.parent
baseline=json.loads((HERE/'baseline-manifest.json').read_text(encoding='utf-8-sig'))
changed=[]; missing=[]; same=[]
for rel,digest in baseline.items():
    p=ROOT/rel
    if not p.exists():missing.append(rel)
    elif hashlib.sha256(p.read_bytes()).hexdigest().upper()==digest:same.append(rel)
    else:changed.append(rel)
allowed_assets={'Machine/AuroraR54.prefab','Scenes/IceResurfacer_AuthoredMachine.unity'}
protected=[rel for rel in baseline if rel.startswith(('Machine/','Arena/','Scenes/','Resources/','Shaders/','RouteRecordings~/'))]
unexpected=[rel for rel in protected if rel in changed and rel not in allowed_assets]
assert not missing,missing
assert not unexpected,unexpected
scene_rel='Scenes/IceResurfacer_AuthoredMachine.unity'
before=(HERE/'Before'/scene_rel).read_text()
after=(ROOT/scene_rel).read_text()
before_without_roller=re.sub(r'    GuideRollerX: [^\n]+','    GuideRollerX: <validated compact position>',before)
after_without_roller=re.sub(r'    GuideRollerX: [^\n]+','    GuideRollerX: <validated compact position>',after)
assert before_without_roller==after_without_roller,'Unexpected scene/calibration/Operator Tuning change'
old_ids=re.findall(r'^--- !u!\d+ &(-?\d+)',(HERE/'Before/Machine/AuroraR54.prefab').read_text(),re.M)
new_ids=re.findall(r'^--- !u!\d+ &(-?\d+)',(ROOT/'Machine/AuroraR54.prefab').read_text(),re.M)
assert len(new_ids)==len(set(new_ids)),'Duplicate prefab IDs'
assert set(old_ids)<=set(new_ids),'Removed authored objects/components'
def documents(path):
    return {int(i):(int(kind),body) for kind,i,body in re.findall(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S)}
old_docs=documents(HERE/'Before/Machine/AuroraR54.prefab')
new_docs=documents(ROOT/'Machine/AuroraR54.prefab')
expected_delta=json.loads((HERE/'asset-delta.json').read_text())
actual_changed=[i for i in old_docs if old_docs[i]!=new_docs[i]]
assert set(actual_changed)==set(expected_delta['changed_original_documents']),actual_changed
assert all(old_docs[i][0] in (1,4,114) for i in actual_changed),'Authored renderer/mesh/collider altered'
for i in set(new_docs)-set(old_docs):
    for target in re.findall(r'\{fileID: (-?\d+)\}',new_docs[i][1]):
        assert int(target)==0 or int(target) in new_docs,(i,'unresolved internal ref',target)
new=[]
for folder,dirs,files in os.walk(ROOT):
    dirs[:]=[d for d in dirs if d not in ('Backups~','Validation~')]
    for name in files:
        rel=(Path(folder)/name).relative_to(ROOT).as_posix()
        if rel not in baseline:new.append(rel)
result={'baseline_files':len(baseline),'unchanged_files':len(same),'protected_assets':len(protected),'protected_assets_unchanged':sum(r in same for r in protected),'modified_existing_files':sorted(changed),'new_files':sorted(new),'removed_files':missing,'unexpected_asset_changes':unexpected,'scene_difference':'GuideRollerX only; Operator Tuning, water, GPS and all other calibration unchanged','prefab_original_document_count':len(old_ids),'prefab_added_document_count':len(new_ids)-len(old_ids)}
result['prefab_changed_existing_document_ids']=actual_changed
result['prefab_reference_integrity']='All new internal references resolve; no original mesh, renderer, collider or object removed'
(HERE/'preservation-audit.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
