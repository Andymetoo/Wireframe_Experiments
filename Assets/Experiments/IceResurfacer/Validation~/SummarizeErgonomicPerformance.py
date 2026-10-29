from pathlib import Path
import csv
import re

root = Path(__file__).resolve().parent
folder = root / 'PerformanceOperatorErgonomics'
resolutions = ['1366x768', '1920x1080']

def read(path, complete=True):
    text = path.read_text(encoding='utf-8-sig')
    lines = text.splitlines()
    header = next(csv.reader([line]) for line in lines if line.startswith('scenario,frames,'))
    header = next(header)
    rows = {}
    for line in lines:
        values = next(csv.reader([line]))
        if len(values) == len(header) and values[1].isdigit():
            rows[values[0]] = dict(zip(header, values))
    if complete:
        assert 'Managed runtime errors: 0' in text, path
    return text, rows

current = [read(folder / ('Performance-' + r + '.csv')) for r in resolutions]
before = read(folder / 'BeforeOptimization/Performance-1366x768.csv')
baseline = [read(root / 'OperatorErgonomicsBefore/PerformanceOperatorAuthenticity' /
                 ('Performance-' + r + '.csv')) for r in resolutions]
assert len(current[0][1]) == len(current[1][1]) == 16
assert current[0][1].keys() == current[1][1].keys()

lines = [
    'AURORA / FINAL RENDERED PERFORMANCE / 29 October 2026',
    'Unity 6000.4.1f1, D3D11, RTX 3050 Laptop GPU, i5-11400H, development Mono player.',
    'Serial hidden offscreen runs, VSync off; original user Editor left open.',
    'One 20 ms simulation step per rendered frame. Actual recorded intervals:',
    '299 for each short workload; 29,999 for sustained operation, after warmup.',
    'Frame measurements include the whole player. CPU_work excludes IMGUI/render.',
    'This measures workload throughput; focused-window latency and Editor FPS',
    'are not established. Screenshot FPS includes capture/automation waits.',
    '',
    'Scenario                      1366 mean / p95 ms    1920 mean / p95 ms',
]
for name, row in current[0][1].items():
    other = current[1][1][name]
    lines.append(f"{name:<29} {float(row['mean_frame_ms']):>7.3f} / {float(row['p95_frame_ms']):>7.3f}"
                 f"       {float(other['mean_frame_ms']):>7.3f} / {float(other['p95_frame_ms']):>7.3f}")

lines += ['', 'Sustained workload and retained cost']
for i, resolution in enumerate(resolutions):
    row = current[i][1]['long_session']
    old = baseline[i][1]['long_session']
    assert row['frames'] == '29999' and row['cutting_frames'] == '29999'
    warnings = re.search(r'Native hidden-window D3D presentation warnings: (\d+)', current[i][0]).group(1)
    lines += [
        f"{resolution}: CPU work {row['cpu_work_ms']} ms; surface refresh {row['surface_refresh_ms_per_frame']} ms/frame;",
        f"  {row['moving_frames']} moving / {row['cutting_frames']} cutting intervals; {row['active_wet_cells']} active wet cells.",
        f"  Work allocation {row['work_bytes_per_frame']} B/frame; whole-frame GC {row['gc_bytes_per_frame']} B/frame;",
        f"  {row['gc_collections']} collections. Native presentation warnings {warnings}; managed errors zero.",
        f"  Earlier feature baseline: {old['mean_frame_ms']} ms mean / {old['p95_frame_ms']} ms p95.",
    ]
lines += [
    'The endurance fixture repeatedly resets its pose to the same working lane',
    'while cutting, washing and flooding. Its accumulated standing water invokes',
    'the new finite redistribution solver. This added physical work remains more',
    'expensive than the earlier feature baseline; baseline parity is not claimed.',
    'The 1920 run has lower whole-frame mean despite similar CPU work and higher',
    'GPU cost. Sequential host/presentation variation prevents interpreting that',
    'difference as a resolution benefit. Both p95 values remain near 23 ms.',
    'Six endurance heap samples remain approximately 84.9-85.6 MB at both sizes.',
    'Live-heap deltas include collection of setup garbage and do not prove leaks.',
    '',
    'Measured optimization, identical physical state',
]
row = current[0][1]['long_session']; old = before[1]['long_session']
for field, label in [('mean_frame_ms', '1366 mean frame'), ('p95_frame_ms', '1366 p95 frame'),
                     ('cpu_work_ms', 'CPU work'), ('surface_refresh_ms_per_frame', 'Surface refresh per frame')]:
    reduction = 100 * (1 - float(row[field]) / float(old[field]))
    lines.append(f"{label}: {old[field]} -> {row[field]} ms ({reduction:.1f}% reduction).")
lines += [
    'Core-only comparison separately improves 1.948 -> 1.086 ms per step.',
    'Six intermediate hashes and the final cell hash match exactly, preserving',
    'water totals, flow order, thresholds, work budget and 10 Hz cadence.',
    'Presentation skips unchanged uploads and water-only snow mesh work while',
    'retaining exact 5 cm state and bounded 65,536-cell visual aging.',
    'All 37 core groups and final Unity suites pass on the optimized source.',
    '',
    'Raw evidence',
    'PerformanceOperatorErgonomics/Performance-1366x768.csv',
    'PerformanceOperatorErgonomics/Performance-1920x1080.csv',
    'PerformanceOperatorErgonomics/BeforeOptimization/ preserves the first 1366 run.',
    'Its corrupt long-session GPU timer is excluded from interpretation; the',
    'final probe rejects impossible/nonfinite positive samples as unavailable.',
    'Water-performance-before/after/comparison.txt provide exact-state CPU evidence.',
    'Native hidden-window warnings bypass the managed callback and are counted',
    'separately in launcher footers. Neither run reports managed runtime errors.',
]
output = '\n'.join(lines) + '\n'
(folder / 'Performance-summary.txt').write_text(output, encoding='utf-8')
print(output)
