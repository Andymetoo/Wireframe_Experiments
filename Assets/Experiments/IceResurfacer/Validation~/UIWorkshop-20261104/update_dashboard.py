from pathlib import Path
import re

p = Path('Assets/Experiments/IceResurfacer/Presentation/IceDashboard.cs')
s = p.read_text(encoding='utf-8-sig')
for old, new in [('Navy', 'PanelPaint'), ('Modal', 'PanelPaint'), ('Mint', 'Sage')]:
    s = re.sub(r'\b' + old + r'\b', new, s)
replacements = {
    'Box(new Rect(408,24,1164,108),PanelPaint);Box(new Rect(408,24,5,108),Amber);':
        'Box(new Rect(408,24,1164,108),PanelPaint);\n            IceUITheme.Rule(new Rect(696,40,1,73));',
    '"AURORA / R-54"': '"AURORA  R-54"',
    '"NORTHLINE / "+Duration(s.Time)': '"NORTHLINE    /    "+Duration(s.Time)',
    '(Mathf.Abs(s.Vehicle.Speed)*3.6f).ToString("0.0"),number,Sage':
        '(Mathf.Abs(s.Vehicle.Speed)*3.6f).ToString("0.0"),number,White',
    '"OPEN CONSOLE [TAB]"': '"Open console  [Tab]"',
    '"CLOSE CONSOLE [TAB]"': '"Close console  [Tab]"',
    '"ROUTES NUM 8"': '"Routes  [Num 8]"',
    '"MESSAGES"': '"Messages"',
    '"HELP F1"': '"Help  F1"',
    'game.Paused?"RESUME":"PAUSE"': 'game.Paused?"Resume":"Pause"',
    '"BRUSH [4]"': '"Brush  [4]"',
    '"PUMP [5]"': '"Pump  [5]"',
    '"BREAKER [6]"': '"Breaker  [6]"',
    '"REFILL HELP"': '"Refill help"',
    '"FINISH RUN"': '"Finish run"',
    '"NEW SESSION"': '"New session"',
    '"CLOSE"': '"Close"',
    'MessagesVisible?"ORDINARY NOTIFICATIONS: ON":"ORDINARY NOTIFICATIONS: OFF"':
        'MessagesVisible?"Routine messages: on":"Routine messages: off"',
    'new Color(.38f,.025f,.08f,.98f)': 'IceUITheme.AlertFace',
    'new Color(.26f,.10f,.065f,.98f)': 'IceUITheme.AlertFace',
    'new Color(.14f,.37f,.31f)': 'IceUITheme.ActiveFace',
    'new Color(.18f, .40f, .33f)': 'IceUITheme.ActiveFace',
    'new Color(.18f,.40f,.33f)': 'IceUITheme.ActiveFace',
    'new Color(.12f, .31f, .27f)': 'IceUITheme.ActiveFace',
    'new Color(.45f,.75f,1)': 'new Color(.61f,.70f,.70f)',
    '"● RECORDING  /  NUMPAD 9 STOPS & SAVES"': '"Recording    /    Numpad 9 stops and saves"',
    'GUI.HorizontalSlider(': 'IceUITheme.HorizontalSlider(',
}
for old, new in replacements.items():
    assert old in s, old
    s = s.replace(old, new)

# The four session documents use paper and ink. Instrument overlays stay painted.
for name, following in [('Welcome', 'Pause'), ('Pause', 'Help'), ('Help', 'Results'), ('Results', 'ReportLine')]:
    a=s.index('        void '+name+'(')
    b=s.index('        void '+following+'(', a)
    part=s[a:b]
    part=re.sub(r'Box\((new Rect\([^\n]+?\)),\s*PanelPaint\);', r'IceUITheme.PaperPanel(\1);', part)
    for old,new in [('White','IceUITheme.Ink'),('Muted','IceUITheme.PaperMuted'),('Sage','IceUITheme.Ink')]:
        part=re.sub(r'\b'+old+r'\b',new,part)
    s=s[:a]+part+s[b:]
a=s.index('        void ReportLine(')
b=s.index('        static string Duration(',a)
part=s[a:b].replace('small, Muted','small, IceUITheme.PaperMuted').replace('heading, White','heading, IceUITheme.Ink').replace('small, Sage','small, IceUITheme.PaperMuted')
s=s[:a]+part+s[b:]
p.write_text(s, encoding='utf-8')
