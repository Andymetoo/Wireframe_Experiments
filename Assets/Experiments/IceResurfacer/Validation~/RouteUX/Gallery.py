from pathlib import Path
import html,json
r=Path(__file__).resolve().parent
sections=[]
for resolution in ['1366x768','1920x1080','960x600']:
    directory=r/('Player-'+resolution)
    report=(directory/('HUD-player-results-'+resolution+'.txt')).read_text(encoding='utf-8-sig')
    assert 'PASS: all player HUD checks.' in report and 'FAIL' not in report
    images=[('Manager-'+resolution+'-two-routes.png','Saved routes, names, dates and approximate lengths'),
            ('Manager-'+resolution+'-gps-ready.png','Loaded recording becomes GPS Ready without an Editor conversion'),
            ('Manager-'+resolution+'-delete-confirmation.png','Explicit deletion confirmation'),
            ('GPS-'+resolution+'-recording.png','Prominent recording indicator'),
            ('GPS-'+resolution+'-seated-magenta.png','Actual seated viewpoint: flat magenta ribbon and machine-only continuation'),
            ('GPS-'+resolution+'-blue-gray.png','Blue-gray wet/uncut ice contrast fixture'),
            ('GPS-'+resolution+'-reflective.png','Reflective resurfaced ice contrast fixture'),
            ('GPS-'+resolution+'-dark-wet.png','Dark wet ice contrast fixture'),
            ('GPS-'+resolution+'-snow.png','Deposited-snow contrast fixture')]
    images += [(p.name,'Supplied actual operator recording / seeded seated render pose') for p in sorted(directory.glob('GPS-*-operator-*.png'))]
    sections.append((resolution,[(str((directory/name).relative_to(r)).replace('\\','/'),caption) for name,caption in images if (directory/name).is_file()]))
page=['<!doctype html><html lang="en"><meta charset="utf-8"><title>Aurora Route Manager refinement</title>',
'<style>body{max-width:1500px;margin:28px auto;padding:0 22px;background:#0c1821;color:#e2eaf0;font:17px/1.5 system-ui}a,h1,h2{color:#ff52c1}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:18px}figure{padding:14px;margin:0;background:#152833}img{width:100%;height:360px;object-fit:contain}figcaption{padding-top:8px}</style>',
'<h1>Aurora R-54 / Route Manager and GPS refinement</h1>',
'<p><a href="../../Documentation/RouteOperator.txt">Record / Save / Load &amp; Follow instructions</a> · <a href="Report.txt">Files changed, investigation, tests and limitations</a> · <a href="Preservation-audit.json">Preservation audit</a></p>',
'<p>Actual native Unity framebuffer captures. Click to inspect original pixels. New route-workflow recordings are labeled control-driven fixtures. Operator screenshots use the two supplied real recordings; only the displayed camera/vehicle pose is seeded. Wet/snow contrast cases are explicit visual test setups. Guidance never drives the machine.</p>']
for title,images in sections:
    page+=['<h2>'+html.escape(title)+'</h2><section>']
    for path,caption in images:page += ['<figure><a href="'+path+'"><img loading="lazy" src="'+path+'" alt="'+html.escape(caption)+'"></a><figcaption>'+html.escape(caption)+'</figcaption></figure>']
    page+=['</section>']
page+=['<p>960×600 checks the same manager at 60% of the reference-canvas width, representing a smaller Editor Game view. Native mouse input targets only the test player; raw Input System keypad events cover Num Lock key state present/absent and name-field focus. Physical Num Lock hardware testing remains a manual check.</p></html>']
(r/'Screenshots.html').write_text('\n'.join(page),encoding='utf-8')
print('Built screenshot gallery for all three resolutions.')
