"""Compare source and protected asset hashes with this task's initial snapshot."""
import hashlib
import json
from pathlib import Path

here = Path(__file__).resolve().parent
root = here.parent.parent
before = json.loads((here / "baseline-manifest.json").read_text(encoding="utf-8-sig"))
changes = []
missing = []
for relative, digest in before.items():
    path = root / relative
    if not path.exists():
        missing.append(relative)
    elif hashlib.sha256(path.read_bytes()).hexdigest().upper() != digest.upper():
        changes.append(relative)
protected = ("Machine\\", "Arena\\", "Scenes\\", "Resources\\", "Shaders\\", "RouteRecordings~\\", "Editor\\")
protected_changes = [name for name in changes + missing if name.startswith(protected)]
result = {"baseline_files": len(before), "modified": sorted(changes), "missing": missing,
          "protected_changes": protected_changes, "passed": not missing and not protected_changes}
(here / "preservation-results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if not result["passed"]:
    raise SystemExit(1)
