from pathlib import Path
import hashlib,re,subprocess
root=Path.cwd()
experiment=root/'Assets/Experiments/IceResurfacer'
git=['git','-c','safe.directory=G:/UnityProjects/Wireframe_Experiments','-c','core.autocrlf=false']
status=subprocess.check_output(git+['status','--porcelain','-z']).decode('utf-8').split('\0')
outside=[line for line in status if line and not line[3:].replace('\\','/').startswith('Assets/Experiments/IceResurfacer/')]
stage=experiment/'Validation~/UnityProject/Assets/Experiments/IceResurfacer'
files=[]
for folder in ['Core','Presentation','Editor','Resources','Shaders','Scenes']:
    files.extend(p for p in (experiment/folder).rglob('*') if p.is_file())
missing=[];duplicates=[];seen={};mismatches=[];matched=0
for path in files:
    if path.suffix!='.meta' and not Path(str(path)+'.meta').exists():missing.append(str(path.relative_to(experiment)))
    if path.suffix=='.meta':
        match=re.search(r'^guid: (\w+)',path.read_text(encoding='utf-8-sig'),re.M)
        if match:
            guid=match.group(1)
            if guid in seen:duplicates.append((seen[guid],str(path)))
            seen[guid]=str(path)
    staged=stage/path.relative_to(experiment)
    if path.suffix=='.meta':continue # Unity may serialize import defaults in its disposable copy.
    if staged.exists() and hashlib.sha256(path.read_bytes()).digest()==hashlib.sha256(staged.read_bytes()).digest():matched+=1
    else:mismatches.append(str(path.relative_to(experiment)))
lines=['AURORA / operator ergonomics preservation audit',f'Source assets matched to isolated validated/build project: {matched}',
       f'Missing source metadata: {len(missing)}',f'Duplicate source GUIDs: {len(duplicates)}',f'Outside-experiment modified/untracked paths: {len(outside)}',
       f'Runtime/editor/asset mismatches: {len(mismatches)}',
       'Unity checks and builds use only Validation~/UnityProject; original Editor not switched/stopped.',
       'No branch, commit, push, shared package or original project-settings changes.']
for title,values in [('Missing',missing),('Duplicates',duplicates),('Outside',outside),('Mismatch',mismatches)]:
    lines.extend(title+': '+str(value) for value in values)
output='\n'.join(lines)+'\n'
(experiment/'Validation~/Preservation-results.txt').write_text(output,encoding='utf-8')
print(output)
raise SystemExit(bool(missing or duplicates or outside or mismatches))
