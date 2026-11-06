"""One-time Phase 1 asset authoring, never invoked by Unity/runtime.

Refuses to replace existing generated assets. Scene/source edits require the
pre-change backup hash. Subsequent visual authoring belongs in Unity.
"""
from pathlib import Path
import hashlib, json, re, tarfile, uuid

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT.parents[2]
UI = ROOT / 'UI'
BACKUP = ROOT / 'Backups~/UIAuthoring-Phase1-20261106'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
NS = uuid.UUID('bc278dfb-614b-4563-bbb4-aa87617069bd')

def guid(path):
    p = Path(path).resolve()
    meta = Path(str(p) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    return uuid.uuid5(NS, str(p.relative_to(PROJECT)).replace('\\', '/')).hex

def meta(path, folder=False):
    p = Path(path)
    m = Path(str(p) + '.meta')
    if m.exists(): return
    importer = 'DefaultImporter'
    fields = ''
    if p.suffix == '.cs':
        importer = 'MonoImporter'
        fields = '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    elif p.suffix == '.prefab': importer = 'PrefabImporter'
    elif p.suffix == '.asset':
        importer = 'NativeFormatImporter'
        fields = '  mainObjectFileID: 11400000\n'
    elif p.suffix == '.asmdef': importer = 'AssemblyDefinitionImporter'
    m.write_text(f'fileFormatVersion: 2\nguid: {guid(p)}\n' + ('folderAsset: yes\n' if folder else '') +
                 f'{importer}:\n  externalObjects: {{}}\n' + fields + '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

def write(path, text):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists(): raise RuntimeError(f'Refusing to overwrite: {path}')
    path.write_text(text, encoding='utf-8', newline='\n')
    meta(path)

def package_guid(filename):
    candidates = list((PROJECT / 'Library/PackageCache').glob('com.unity.ugui@*/**/' + filename + '.meta'))
    candidates += list((PROJECT / 'Library/PackageCache').glob('com.unity.inputsystem@*/**/' + filename + '.meta'))
    candidates = [p for p in candidates if '/Tests/' not in str(p).replace('\\','/')]
    assert len(candidates) == 1, (filename, candidates)
    return re.search(r'^guid: (\w+)', candidates[0].read_text(), re.M)[1]

def ref(fileid=0, asset=None):
    native = asset and asset in [FONT, guid(THEME)]
    return '{fileID: ' + str(fileid) + (', guid: ' + asset + ', type: ' + ('2' if native else '3') if asset else '') + '}'

def color(r,g,b,a=255): return f'{{r: {r/255:.7f}, g: {g/255:.7f}, b: {b/255:.7f}, a: {a/255:.7f}}}'

PANEL=color(211,211,199); TEXT=color(24,26,22); MUTED=color(55,57,50); ACCENT=color(56,78,32); INSET=color(179,182,169)
FONT='8f586378b4e144a9851e7b34d9b748ee'
THEME=UI/'Themes/Aurora UI Theme.asset'
PIXEL=UI/'Artwork/Placeholder White.png'
SCRIPTS={x:guid(UI/'Runtime'/f'{x}.cs') for x in ['IceAuthoredHud','IceHudWidget','IceUIStyle','IceUIStyleSheet']}
BUILTIN={x:package_guid(x+'.cs') for x in ['TextMeshProUGUI','CanvasScaler','Image','GraphicRaycaster','Button','EventSystem','InputSystemUIInputModule']}

class Asset:
    def __init__(self): self.nodes=[]; self.extra=[]; self.next=10000
    def id(self): self.next+=1; return self.next
    def node(self,name,parent=None,rect=(0,0,1600,900),stretch=False):
        n={'go':self.id(),'rt':self.id(),'name':name,'parent':parent,'rect':rect,'stretch':stretch,'children':[],'components':[]}
        self.nodes.append(n)
        if parent: parent['children'].append(n)
        return n
    def component(self,n,kind,body,script=None):
        ident=self.id(); n['components'].append(ident)
        base=f'--- !u!{kind} &{ident}\n'+({114:'MonoBehaviour',223:'Canvas',225:'CanvasGroup',222:'CanvasRenderer'}[kind])+':\n'
        base+='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
        base+=f'  m_GameObject: {ref(n["go"])}\n'
        if kind!=222: base+='  m_Enabled: 1\n'
        if kind==114: base+=f'  m_EditorHideFlags: 0\n  m_Script: {ref(11500000,script)}\n  m_Name: \n  m_EditorClassIdentifier: \n'
        self.extra.append((ident,base+body)); return ident
    def mono(self,n,script,body):return self.component(n,114,body,script)
    def renderer(self,n): self.component(n,222,'  m_CullTransparentMesh: 1\n')
    def image(self,n,paint=PANEL,raycast=False,filled=False):
        self.renderer(n)
        return self.mono(n,BUILTIN['Image'],f'''  m_Material: {{fileID: 0}}
  m_Color: {paint}
  m_RaycastTarget: {int(raycast)}
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {ref(21300000,guid(PIXEL)) if filled else ref()}
  m_Type: {3 if filled else 0}
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 0
  m_FillAmount: {0.25 if filled else 1}
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
''')
    def style(self,n,text=0,image=0,button=0,font=1,tone=0,sprite=0,apply_sprite=False):
        return self.mono(n,SCRIPTS['IceUIStyle'],f'''  theme: {ref(11400000,guid(THEME))}
  text: {ref(text)}
  image: {ref(image)}
  button: {ref(button)}
  fontRole: {font}
  colorRole: {tone}
  spriteRole: {sprite}
  applyFont: {int(bool(text))}
  applyColor: 1
  applySprite: {int(apply_sprite)}
  applyFontSize: 0
  applyButtonStates: {int(bool(button))}
''')
    def text(self,parent,name,caption,rect,size=16,tone=0,font=1,stretch_width=False):
        n=self.node(name,parent,rect)
        if stretch_width: n['width_stretch']=True
        self.renderer(n)
        txt=self.mono(n,BUILTIN['TextMeshProUGUI'],f'''  m_Material: {{fileID: 0}}
  m_Color: {TEXT if tone==0 else MUTED}
  m_RaycastTarget: 0
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: {json.dumps(caption)}
  m_isRightToLeft: 0
  m_fontAsset: {ref(11400000,FONT)}
  m_sharedMaterial: {ref(2180264,FONT)}
  m_fontSharedMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4279638552
  m_fontColor: {TEXT if tone==0 else MUTED}
  m_enableVertexGradient: 0
  m_overrideHtmlColors: 0
  m_fontSize: {size}
  m_fontSizeBase: {size}
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 8
  m_fontSizeMax: 72
  m_fontStyle: 0
  m_HorizontalAlignment: 1
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_paragraphSpacing: 0
  m_TextWrappingMode: 0
  m_enableWordWrapping: 0
  m_overflowMode: 0
  m_isRichText: 0
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {{x: 0, y: 0, z: 0, w: 0}}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {{fileID: 0}}
  m_maskOffset: {{x: 0, y: 0, z: 0, w: 0}}
''')
        self.style(n,text=txt,font=font,tone=tone)
        return txt
    def widget(self,parent,name,kind,rect,label,value,detail='',meter=False):
        n=self.node(name,parent,rect); w,h=rect[2:]
        bg=self.image(n,raycast=True)
        action=0
        if kind==2:
            action=self.mono(n,BUILTIN['Button'],f'''  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.95, g: 0.95, b: 0.95, a: 1}}
    m_PressedColor: {{r: 0.8, g: 0.8, b: 0.8, a: 1}}
    m_SelectedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_Interactable: 1
  m_TargetGraphic: {ref(bg)}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
''')
        self.style(n,image=bg,button=action,tone=2,sprite=1 if action else 0,apply_sprite=True)
        fill=0
        if kind in [6,7]:
            lbl=self.text(n,'Label',label,(12,2,135 if kind==6 else 90,28),16)
            val=self.text(n,'Value',value,(150 if kind==6 else 100,2,270 if kind==6 else 190,28),16,font=1)
            det=self.text(n,'Detail',detail,(430 if kind==6 else 300,2,w-(442 if kind==6 else 312),28),16,tone=1)
        elif kind==0:
            lbl=self.text(n,'Label',label,(20,8,w-30,38),28,font=0,stretch_width=True)
            val=self.text(n,'Value',value,(20,49,w-30,23),16,tone=1,stretch_width=True)
            det=0
        elif kind==1:
            lbl=self.text(n,'Label',label,(10,4,w-20,16),12,tone=1,stretch_width=True)
            val=self.text(n,'Value',value,(10,19,w-20,33),32,font=2,stretch_width=True)
            det=self.text(n,'Detail',detail,(10,54,w-16,18),13,tone=1,stretch_width=True)
        else:
            lbl=self.text(n,'Label',label,(14,7,w-28,20),15,tone=1,stretch_width=True)
            val=self.text(n,'Value',value,(14,29,w-28,29),24,font=2,stretch_width=True)
            det=self.text(n,'Detail',detail,(14,58,w-28,12),9,tone=1,stretch_width=True)
        if meter:
            bar=self.node('Meter Background',n,(14,h-7,w-28,4)); bar['bottom_stretch']=True
            im=self.image(bar,INSET); self.style(bar,image=im,tone=3,sprite=2,apply_sprite=True)
            fn=self.node('Fill',bar,stretch=True)
            fill=self.image(fn,ACCENT,filled=True); self.style(fn,image=fill,tone=5,sprite=3,apply_sprite=True)
            preview_fill={2: .42, 3: 1, 4: 1, 5: .25}[kind]
            self.extra=[(i,s.replace('  m_FillAmount: 0.25\n',f'  m_FillAmount: {preview_fill}\n')) if i==fill else (i,s) for i,s in self.extra]
        comp=self.mono(n,SCRIPTS['IceHudWidget'],f'''  kind: {kind}
  label: {ref(lbl)}
  value: {ref(val)}
  detail: {ref(det)}
  fill: {ref(fill)}
  action: {ref(action)}
''')
        return n,comp
    def serialize(self):
        result=HEADER
        for n in self.nodes:
            result+=f'''--- !u!1 &{n['go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {ref(n['rt'])}
'''
            for c in n['components']: result+=f'  - component: {ref(c)}\n'
            result+=f'''  m_Layer: 5
  m_Name: {n['name']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{n['rt']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {ref(n['go'])}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
'''
            result+='  m_Children:' + ('\n' if n['children'] else ' []\n')
            for c in n['children']:result+=f'  - {ref(c["rt"])}\n'
            result+=f'  m_Father: {ref(n["parent"]["rt"] if n["parent"] else 0)}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
            x,y,w,h=n['rect']; amin=(0,1);amax=(0,1);pivot=(0,1);pos=(x,-y);size=(w,h)
            if n['stretch']: amin=(0,0);amax=(1,1);pivot=(.5,.5);pos=(0,0);size=(0,0)
            if n.get('width_stretch'):amax=(1,1);size=(-x-(n['parent']['rect'][2]-x-w),h)
            if n.get('bottom_stretch'):amin=(0,0);amax=(1,0);pivot=(0,0);pos=(x,3);size=(-28,h)
            result+=f'''  m_AnchorMin: {{x: {amin[0]}, y: {amin[1]}}}
  m_AnchorMax: {{x: {amax[0]}, y: {amax[1]}}}
  m_AnchoredPosition: {{x: {pos[0]}, y: {pos[1]}}}
  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}
  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}
'''
        return result+''.join(s for _,s in self.extra)

def import_typography():
    package=next((PROJECT/'Library/PackageCache').glob('com.unity.ugui@*/Package Resources/TMP Essential Resources.unitypackage'))
    keep={'LiberationSans SDF.asset','LiberationSans SDF - Fallback.asset','LiberationSans.ttf','LiberationSans - OFL.txt','TMP Settings.asset','Default Style Sheet.asset','LineBreaking Leading Characters.txt','LineBreaking Following Characters.txt','TMP_SDF-Mobile.shader','TMPro_Properties.cginc','TMPro.cginc','TMPro_Mobile.cginc'}
    with tarfile.open(package) as archive:
        for m in archive.getmembers():
            if not m.name.endswith('/pathname'):continue
            source=Path(archive.extractfile(m).read().decode())
            if source.name not in keep:continue
            ident=m.name.split('/')[0]
            dest=UI/'Typography'/source.relative_to('Assets/TextMesh Pro')
            dest.parent.mkdir(parents=True,exist_ok=True)
            assert not dest.exists(), dest
            data=archive.extractfile(ident+'/asset').read()
            if source.name=='TMP Settings.asset':
                data=re.sub(rb'm_defaultSpriteAsset: \{fileID: 11400000, guid: c41005c129ba4d66911b75229fd70b45,\s*type: 2\}',b'm_defaultSpriteAsset: {fileID: 0}',data)
            dest.write_bytes(data)
            Path(str(dest)+'.meta').write_bytes(archive.extractfile(ident+'/asset.meta').read())

def create_theme():
    body=HEADER+f'''--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {ref(11500000,SCRIPTS['IceUIStyleSheet'])}
  m_Name: Aurora UI Theme
  m_EditorClassIdentifier: 
  primaryFont: {ref(11400000,FONT)}
  secondaryFont: {ref(11400000,FONT)}
  numericFont: {ref(11400000,FONT)}
  primaryFontSize: 28
  secondaryFontSize: 16
  numericFontSize: 24
  panelColor: {PANEL}
  insetColor: {INSET}
  buttonColor: {color(236,233,218)}
  textColor: {TEXT}
  mutedColor: {MUTED}
  accentColor: {ACCENT}
  warningColor: {color(126,68,9)}
  dangerColor: {color(146,36,24)}
  borderColor: {color(98,101,88)}
  paperColor: {color(235,232,217)}
  panelSprite: {{fileID: 0}}
  buttonSprite: {{fileID: 0}}
  meterBackgroundSprite: {{fileID: 0}}
  meterFillSprite: {ref(21300000,guid(PIXEL))}
  buttonSpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  buttonColors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.95, g: 0.95, b: 0.95, a: 1}}
    m_PressedColor: {{r: 0.8, g: 0.8, b: 0.8, a: 1}}
    m_SelectedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  commonSpacing: {{x: 12, y: 8}}
  commonPadding: {{x: 14, y: 14, z: 7, w: 7}}
  borderThickness: 1
'''
    write(THEME,body)

def create_prefabs():
    a=Asset(); root=a.node('Aurora UI',stretch=True)
    canvas=a.component(root,223,'''  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {fileID: 0}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 25
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 10
  m_TargetDisplay: 0
''')
    a.mono(root,BUILTIN['CanvasScaler'],'''  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {x: 1600, y: 900}
  m_ScreenMatchMode: 1
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
''')
    ray=a.mono(root,BUILTIN['GraphicRaycaster'],'''  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295
''')
    hud=a.node('Driving HUD',root,stretch=True)
    group=a.component(hud,225,'  m_Alpha: 1\n  m_Interactable: 1\n  m_BlocksRaycasts: 1\n  m_IgnoreParentGroups: 0\n')
    specs=[('Identity',0,(408,24,300,76),'AURORA  R-54','NORTHLINE / 04:32','',False),
           ('Speed',1,(708,24,132,76),'VEHICLE SPEED','5.0','km/h FORWARD',False),
           ('Machine Load',2,(840,24,145,76),'MACHINE LOAD','42%','Click / F3 diagnostics',True),
           ('Flood Water',3,(985,24,180,76),'FLOOD WATER','738 L','',True),
           ('Wash Water',4,(1165,24,180,76),'WASH WATER','310 L','',True),
           ('Snow Tank',5,(1345,24,227,76),'SNOW TANK','25% / 0.89 m3','',True),
           ('Conditioner',6,(408,100,577,32),'CONDITIONER','RAISED','100% raised',False),
           ('Blade',7,(985,100,587,32),'BLADE','0.10 mm SET','0.000 mm CONTACT',False)]
    widgets=[a.widget(hud,*s)[1] for s in specs]
    badge=a.node('Design Preview - hidden in Play',hud,(408,135,590,22))
    a.text(badge,'Preview Notice','DESIGN PREVIEW - sample values; live data replaces these in Play',(0,0,590,22),14,tone=1)
    controller=a.mono(hud,SCRIPTS['IceAuthoredHud'],f'''  game: {{fileID: 0}}
  canvas: {ref(canvas)}
  visibility: {ref(group)}
  raycaster: {ref(ray)}
  widgets:
'''+''.join(f'  - {ref(w)}\n' for w in widgets)+f'  previewBadge: {ref(badge["go"])}\n  refreshInterval: 0.1\n')
    for name in ['Machinery Controls','Surface Survey','Navigation','Messages','Menus','Diagnostics']:
        a.node(name,root,stretch=True)
    event=a.node('UI Event System',root)
    a.mono(event,BUILTIN['EventSystem'],'''  m_FirstSelected: {fileID: 0}
  m_sendNavigationEvents: 0
  m_DragThreshold: 10
''')
    a.mono(event,BUILTIN['InputSystemUIInputModule'],'''  m_SendPointerHoverToParent: 1
  m_MoveRepeatDelay: 0.5
  m_MoveRepeatRate: 0.1
  m_XRTrackingOrigin: {fileID: 0}
  m_DeselectOnBackgroundClick: 1
  m_PointerBehavior: 0
  m_CursorLockBehavior: 0
  m_ScrollDeltaPerTick: 6
  m_ActionsAsset: {fileID: 0}
  m_PointAction: {fileID: 0}
  m_MoveAction: {fileID: 0}
  m_SubmitAction: {fileID: 0}
  m_CancelAction: {fileID: 0}
  m_LeftClickAction: {fileID: 0}
  m_MiddleClickAction: {fileID: 0}
  m_RightClickAction: {fileID: 0}
  m_ScrollWheelAction: {fileID: 0}
  m_TrackedDevicePositionAction: {fileID: 0}
  m_TrackedDeviceOrientationAction: {fileID: 0}
''')
    prefab=UI/'Prefabs/Aurora UI.prefab'; write(prefab,a.serialize())
    for name,spec in [('Aurora Meter',specs[3]),('Aurora Diagnostics Button',specs[2]),('Aurora Status',specs[6])]:
        b=Asset(); args=list(spec); args[0]=name;args[2]=(0,0,spec[2][2],spec[2][3]);b.widget(None,*args)
        write(UI/'Prefabs/Widgets'/f'{name}.prefab',b.serialize())
    return prefab,root['rt'],controller

def checked_edit(rel,change):
    path=ROOT/rel; old=path.read_bytes(); expected=(BACKUP/rel).read_bytes()
    assert old==expected, f'Concurrent modification detected: {path}'
    text=old.decode('utf-8-sig'); new=change(text)
    path.write_bytes(new.encode('utf-8'))

def integrate(prefab,rootid,controller):
    def scene_edit(s):
        assert 'Aurora UI' not in s
        eol='\r\n' if '\r\n' in s else '\n'
        extra=f'''--- !u!1001 &1905400000
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {ref(controller,guid(prefab))}
      propertyPath: game
      value:
      objectReference: {{fileID: 1002}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {ref(100100000,guid(prefab))}
--- !u!224 &1905400001 stripped
RectTransform:
  m_CorrespondingSourceObject: {ref(rootid,guid(prefab))}
  m_PrefabInstance: {{fileID: 1905400000}}
  m_PrefabAsset: {{fileID: 0}}
'''.replace('\n',eol)
        marker='--- !u!1660057539 &9223372036854775807'
        assert s.count(marker)==1
        s=s.replace(marker,extra+marker)
        return s+f'  - {{fileID: 1905400001}}{eol}'
    checked_edit('Scenes/IceResurfacer_AuthoredMachine.unity',scene_edit)
    def asm(s):
        return s.replace('["Unity.InputSystem"]','["Unity.InputSystem", "UnityEngine.UI", "Unity.TextMeshPro"]')
    checked_edit('IceResurfacer.Runtime.asmdef',asm)
    def game(s):
        target='        public bool HudHidden {get;private set;}'
        assert s.count(target)==1
        return s.replace(target,target+'\n        // Registered by the persistent scene UI; procedural scenes leave this null.\n        public IceAuthoredHud AuthoredHud { get; set; }\n        public bool HasAuthoredHud => AuthoredHud != null && AuthoredHud.IsAvailable;')
    checked_edit('Presentation/IceResurfacerGame.cs',game)
    def dashboard(s):
        target='            if(new Rect(408,24,1164,108).Contains(p))return true;'
        assert s.count(target)==1
        s=s.replace(target,'            if(game.HasAuthoredHud)\n            { if(game.AuthoredHud.ContainsPointer(screenPoint))return true; }\n            else if(new Rect(408,24,1164,108).Contains(p))return true;')
        target='                Header(game);'; assert s.count(target)==1
        s=s.replace(target,'                if(!game.HasAuthoredHud)Header(game);')
        target='                if(Event.current.type==EventType.Repaint)game.RouteGuide?.DrawSilhouette(width,height);'
        assert s.count(target)==1
        s=s.replace(target,'                // A visually moved uGUI panel owns mouse events over legacy controls too.\n                // Keep a legacy drag that began elsewhere until its normal release.\n                if(!pointerCaptured&&(Event.current.isMouse||Event.current.type==EventType.ScrollWheel)&&game.HasAuthoredHud&&Mouse.current!=null&&\n                    game.AuthoredHud.ContainsPointer(Mouse.current.position.ReadValue()))Event.current.Use();\n'+target)
        return s
    checked_edit('Presentation/IceDashboard.cs',dashboard)
    def service(s):
        target='            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame)interact=true;'
        assert s.count(target)==1
        return s.replace(target,'            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame&&\n                !(game.HasAuthoredHud&&game.AuthoredHud.ContainsPointer(mouse.position.ReadValue())))interact=true;')
    checked_edit('Presentation/IceWaterService.cs',service)

def create_pixel():
    import struct,zlib
    def chunk(tag,data):return struct.pack('!I',len(data))+tag+data+struct.pack('!I',zlib.crc32(tag+data)&0xffffffff)
    raw=b''.join(b'\0'+b'\xff\xff\xff\xff'*8 for _ in range(8))
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',8,8,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
    PIXEL.parent.mkdir(parents=True,exist_ok=True)
    assert not PIXEL.exists()
    PIXEL.write_bytes(png)
    Path(str(PIXEL)+'.meta').write_text(f'''fileFormatVersion: 2
guid: {guid(PIXEL)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {uuid.uuid5(NS,'placeholder-sprite').hex}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
''')

if __name__=='__main__':
    create_pixel();import_typography();create_theme();integrate(*create_prefabs())
    for p in sorted(UI.rglob('*')):
        if p.suffix!='.meta':meta(p,p.is_dir())
    meta(UI,True)
    print('Authored UI prefabs, theme, TMP placeholders, scene instance and minimal bridges written.')
