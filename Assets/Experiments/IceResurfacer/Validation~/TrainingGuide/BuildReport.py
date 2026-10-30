"""Assemble local validation evidence without changing simulation or source data."""
from pathlib import Path
import csv
import html
import io
import json

root = Path(__file__).resolve().parent
experiment = root.parents[1]
for resolution in ['1366x768', '1920x1080']:
    report = (root / ('Player-' + resolution) / ('HUD-player-results-' + resolution + '.txt')).read_text(encoding='utf-8-sig')
    assert 'PASS: all player HUD checks.' in report and 'FAIL' not in report
    assert 'runtime missed-area view excludes all future unconfirmed passes' in report
assert 'PASS: 39 deterministic simulation scenarios.' in (root / 'Core-validation.txt').read_text(encoding='utf-8-sig')
assert 'Final runtime error/exception count: 0' in (root / 'Unity-regression-summary.txt').read_text()
results = []
for resolution in ['1366x768', '1920x1080']:
    path = root / 'Performance' / ('Performance-' + resolution + '.csv')
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    header = next(s for s in lines if s.startswith('scenario,frames,'))
    rows = list(csv.DictReader(io.StringIO('\n'.join([header] + [s for s in lines if s.startswith('training_guide_')]))))
    for row in rows: row['resolution'] = resolution
    results.extend(rows)

sections = [
    ('Shared Editor authoring-preview exports', [
        ('Editor-path-preview.png', 'Raw gray, fitted vehicle cyan, modeled conditioner gold. Green start / orange end.'),
        ('Editor-intended-footprints.png', 'Intended blade and water geometry, including physical flood fringe and overlap.'),
        ('Editor-recorded-blade.png', 'Blade footprint from the deterministic recorded fixture.'),
        ('Editor-recorded-water.png', 'Water contact estimate from recorded valve state; not measured fluid flux.'),
        ('Editor-missed-diagnostic.png', 'Editor comparison of complete intended plan and recorded processing geometry.')]),
    ('Actual native player captures', [
        (f'Player-{r}/Training-{r}-{state}.png', f'{r}: {caption}')
        for r in ['1366x768', '1920x1080']
        for state, caption in [('overview', 'full-route authoring interface'), ('seated', 'seated guide and limited Surface Survey'),
                               ('standing', 'standing cockpit guide'), ('turn-chase', 'upcoming curve in chase view'),
                               ('depth-occlusion', 'actual training shader: green exposed line, red opaque solid occludes its center')]])]

page = ['<!doctype html><html lang="en"><meta charset="utf-8"><title>Aurora R-54 training guide validation</title>',
        '<style>body{margin:32px auto;max-width:1400px;padding:0 22px;background:#0c1a23;color:#dcecee;font:16px/1.5 system-ui}h1,h2{color:#70e3cb}a{color:#70e3cb}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:20px}figure{margin:0;padding:12px;background:#142b36;border:1px solid #36515c}img{width:100%;height:360px;object-fit:contain}figcaption{min-height:60px}table{border-collapse:collapse}th,td{padding:8px 14px;border:1px solid #36515c;text-align:left}</style>',
        '<h1>Aurora R-54 / editable routes and advisory guide</h1>',
        '<p>All routes here are labeled deterministic tests. No actual operator recording was supplied. The original F5 generated reference remains the startup default. Click any image for its original resolution.</p>',
        '<p><a href="Validation-report.txt">Validation results and modified files</a> · <a href="../../Documentation/TrainingGuide.txt">Recording / editing / guide instructions</a> · <a href="Fixture.guide.json">Portable test guide</a> · <a href="Fixture.route.json">Unmodified raw test source</a></p>']
for title, figures in sections:
    page += ['<h2>' + html.escape(title) + '</h2><section>']
    for path, caption in figures:
        if not (root / path).is_file(): raise RuntimeError('Missing screenshot ' + path)
        page += [f'<figure><a href="{path}"><img loading="lazy" src="{path}" alt="{html.escape(caption)}"></a><figcaption>{html.escape(caption)}</figcaption></figure>']
    page += ['</section>']
page += ['<p>Editor images are direct exports of the shared authoring-preview texture, not captures of the whole Editor window. Native images include real IMGUI and world rendering. Their camera/vehicle poses are explicit rendering fixtures; they are not claims of a manually completed operator run.</p>',
         '<h2>Rendered matched workloads</h2><table><tr><th>Resolution</th><th>Guide</th><th>Frames</th><th>Mean / p95 frame ms</th><th>CPU work ms</th><th>GC B/frame</th></tr>']
for row in results:
    page += [f'<tr><td>{row["resolution"]}</td><td>{row["scenario"]}</td><td>{row["frames"]}</td><td>{row["mean_frame_ms"]} / {row["p95_frame_ms"]}</td><td>{row["cpu_work_ms"]}</td><td>{row["gc_bytes_per_frame"]}</td></tr>']
page += ['</table><p>Hidden development-player throughput on RTX 3050 Laptop / D3D11. Normal controls drive both workloads. Setup/fitting is excluded. CPU work includes physics and guide observation; frame time includes rendering, IMGUI and map. Short samples and order/warmup variance prevent interpreting a faster row as a guide speedup. Native hidden-window presentation warnings prevent focused-window latency claims. See CSV files for full counters.</p></html>']
(root / 'Screenshots.html').write_text('\n'.join(page), encoding='utf-8')

audit = json.loads((root / 'Preservation-audit.json').read_text())
summary = {'core_scenarios': 39, 'recorded_fixture_samples': 2092, 'fitted_samples': 14547, 'contexts': 29,
           'guide_length_m': 433.928038, 'minimum_radius_m': 4.497955, 'required_radius_m': 4.485012,
           'maximum_nearest_raw_sample_deviation_m': .145836, 'look_ahead_m': 28, 'near_line_hidden_m': 4,
           'native_resolutions': ['1366x768', '1920x1080'], 'performance': results, 'preservation': audit}
(root / 'Results-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('Built Screenshots.html and Results-summary.json')
