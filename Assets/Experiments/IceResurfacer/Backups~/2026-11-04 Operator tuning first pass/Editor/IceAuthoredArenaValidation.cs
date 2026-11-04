using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Phase 2 proof, run only in the isolated validation project against a copy of the user's
    /// real authored machine and scene:
    ///   Baseline -> SetUp -> Verify -> WriteEdits -> VerifyEdits -> Recovery
    /// Every step compares the authored machine's full fingerprint with the baseline.
    /// </summary>
    [InitializeOnLoad]
    public static class IceAuthoredArenaValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string StageKey="Northline.AuthoredArena.Stage", HashKey="Northline.AuthoredArena.Hashes";
        const string ResultsPath=Root+"Validation~/AuthoredArena-results.txt";
        const string Output=Root+"Validation~/AuthoredArena/";
        const string FingerprintPath=Output+"machine-fingerprint-baseline.txt";
        const string EditsPath=Output+"arena-edits.json";
        const string BannerSection="Banners and signage", LightSection="Steel trusses and arena lighting";
        static int frame;

        [Serializable] sealed class Pose { public Vector3 position; public Quaternion rotation; public Vector3 scale; }
        [Serializable] sealed class Edits
        {
            public Pose advertLocal, housingLocal, diffuserLocal, lightLocal;   // originals, for restore
            public string advertMaterialOriginal; public Color seatOriginal, seatAuthored, lightColorOriginal;
            public float lightIntensityOriginal;
            public Vector3 advertWorld, housingWorld, lightWorld;               // expected after the edit (game space)
            public string advertMaterialAuthored; public float lightIntensity; public Color lightColor;
        }

        static IceAuthoredArenaValidation()
        {
            if(Application.isBatchMode&&SessionState.GetString(StageKey,"")!="")
            { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
        }

        // ---- Entry points (-executeMethod) ------------------------------------------------

        /// <summary>Before any migration: the user's authored machine as found, and Edit Mode views.</summary>
        public static void Baseline()
        {
            Run(() =>
            {
                RequireIsolated();Start("Baseline (before arena migration)");
                Check(!AssetDatabase.IsValidFolder(IceArenaBaker.ArenaFolder),"no arena assets exist yet");
                var game=OpenAuthored();
                Check(game.GetComponentInChildren<IceArenaRig>(true)==null,"authored scene has no arena yet (arena generated at runtime)");
                var rig=MachineRig(game);
                Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.gameObject)==IceMachineBaker.PrefabPath,"authored machine is an instance of "+IceMachineBaker.PrefabPath);
                rig.Collect();Pass("authored machine binds every IceWorld part");
                Directory.CreateDirectory(Output);
                File.WriteAllText(FingerprintPath,MachineFingerprint(game));
                Pass("machine fingerprint recorded: "+File.ReadAllLines(FingerprintPath).Length+" lines (files, prefab parts, materials, scene instance)");
                UserEdits(game,rig);
                Capture("Before");
                Exit(0);
            });
        }

        public static void SetUp()
        {
            Run(() =>
            {
                RequireIsolated();Start("SetUp (first-time arena)");
                var game=OpenAuthored();
                var arena=IceArenaBaker.SetUpFirstArena(game);
                Pass("generated "+IceArenaBaker.ArenaFolder+" and placed "+arena.name+" in the authored scene");
                Check(IceAuthoredRecovery.Backups().Count>=1,"scene was backed up before the arena was added");
                try { IceArenaBaker.SetUpFirstArena(IceMachineBaker.AuthoredGame());Check(false,"second setup must be refused"); }
                catch(InvalidOperationException e) { Pass("second first-time setup refused: "+e.Message); }
                game=OpenAuthored();
                SameFingerprint(game,"after arena setup");
                Capture("After");
                Exit(0);
            });
        }

        public static void Verify()
        {
            Run(() =>
            {
                RequireIsolated();Start("Verify");
                var game=OpenAuthored();var arena=ArenaRig(game);
                CheckEditMode(game,arena);
                CheckEquivalence(game,arena);
                SameFingerprint(game,"before Play");
                SessionState.SetString(HashKey,JsonUtility.ToJson(new HashList(AssetHashes())));
                Enter("verify-play");
            });
        }

        public static void WriteEdits()
        {
            Run(() =>
            {
                RequireIsolated();Start("WriteEdits");
                var game=OpenAuthored();
                var record=new Edits();
                string banners=IceArenaBaker.SectionPrefabPath(IceArenaBaker.ArenaFolder,BannerSection);
                var root=PrefabUtility.LoadPrefabContents(banners);
                try
                {
                    // Board-adjacent cosmetic: an advertisement panel on the dasher, moved and re-materialled.
                    var advert=Named(root.transform,"Aurora board advertisement");
                    record.advertLocal=Local(advert);
                    var renderer=advert.GetComponent<MeshRenderer>();
                    record.advertMaterialOriginal=AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                    advert.localPosition+=new Vector3(0,0,.6f);
                    record.advertMaterialAuthored=IceArenaBaker.MaterialFolder+"/Signal vermilion.mat";
                    renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(record.advertMaterialAuthored);
                    Check(renderer.sharedMaterial!=null,"replacement material exists");
                    PrefabUtility.SaveAsPrefabAsset(root,banners,out bool saved);Check(saved,"banner section prefab saved");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                string lights=IceArenaBaker.SectionPrefabPath(IceArenaBaker.ArenaFolder,LightSection);
                root=PrefabUtility.LoadPrefabContents(lights);
                try
                {
                    // A ceiling fixture: housing, diffuser and its real Light, moved together; light retuned.
                    var light=Named(root.transform,"Arena pool light");
                    Transform housing=null,diffuser=null;
                    foreach(Transform child in root.transform)
                    {
                        if(child.name=="Arena fixture housing"&&housing==null&&Vector3.Distance(child.localPosition,light.localPosition)<.3f)housing=child;
                        if(child.name=="Arena fixture diffuser"&&diffuser==null&&Vector3.Distance(child.localPosition,light.localPosition)<.3f)diffuser=child;
                    }
                    Check(housing!=null&&diffuser!=null,"found the fixture that carries the first pool light");
                    record.housingLocal=Local(housing);record.diffuserLocal=Local(diffuser);record.lightLocal=Local(light);
                    var move=new Vector3(1.5f,-.4f,0);
                    housing.localPosition+=move;diffuser.localPosition+=move;light.localPosition+=move;
                    var component=light.GetComponent<Light>();
                    record.lightIntensityOriginal=component.intensity;record.lightColorOriginal=component.color;
                    record.lightIntensity=31;record.lightColor=new Color(1,.85f,.6f);
                    component.intensity=record.lightIntensity;component.color=record.lightColor;
                    PrefabUtility.SaveAsPrefabAsset(root,lights,out bool saved);Check(saved,"lighting section prefab saved");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                var seat=AssetDatabase.LoadAssetAtPath<Material>(IceArenaBaker.MaterialFolder+"/Grandstand blue.mat");
                record.seatOriginal=seat.GetColor("_BaseColor");record.seatAuthored=new Color(.55f,.12f,.16f,1);
                seat.SetColor("_BaseColor",record.seatAuthored);EditorUtility.SetDirty(seat);AssetDatabase.SaveAssets();
                game=OpenAuthored();var arena=ArenaRig(game);
                record.advertWorld=GameSpace(game,Find(arena.transform,"Aurora board advertisement").position);
                record.housingWorld=GameSpace(game,Moved(arena.transform,"Arena fixture housing",record.housingLocal).position);
                record.lightWorld=GameSpace(game,Moved(arena.transform,"Arena pool light",record.lightLocal).position);
                File.WriteAllText(EditsPath,JsonUtility.ToJson(record,true));
                Pass("edited in prefabs: advertisement moved 0.6 m and re-materialled, ceiling fixture and its Light moved 1.5 m, light retuned, grandstand material recoloured");
                Exit(0);
            });
        }

        public static void VerifyEdits()
        {
            Run(() =>
            {
                RequireIsolated();Start("VerifyEdits");
                var record=EditRecord();
                var game=OpenAuthored();var arena=ArenaRig(game);
                // Fresh editor process: everything below comes from disk.
                var advert=Find(arena.transform,"Aurora board advertisement");
                Near(GameSpace(game,advert.position),record.advertWorld,"reopened: board advertisement at its edited position");
                Check(AssetDatabase.GetAssetPath(advert.GetComponent<MeshRenderer>().sharedMaterial)==record.advertMaterialAuthored,"reopened: board advertisement keeps its new material");
                var light=Moved(arena.transform,"Arena pool light",record.lightLocal);
                Near(GameSpace(game,light.position),record.lightWorld,"reopened: ceiling light moved with its fixture");
                Check(Mathf.Approximately(light.GetComponent<Light>().intensity,record.lightIntensity)&&light.GetComponent<Light>().color==record.lightColor,"reopened: Light intensity and colour persisted");
                Near(GameSpace(game,Moved(arena.transform,"Arena fixture housing",record.housingLocal).position),record.housingWorld,"reopened: fixture housing at its edited position");
                Check(AssetDatabase.LoadAssetAtPath<Material>(IceArenaBaker.MaterialFolder+"/Grandstand blue.mat").GetColor("_BaseColor")==record.seatAuthored,"reopened: material colour edit persisted in its asset");
                NoDrift(arena,game,"cosmetic edits leave the physical rink in agreement");
                DriftDetection(game,arena);
                game=OpenAuthored();
                SameFingerprint(game,"after arena edits");
                CheckMaterialsClean("before Play");
                SessionState.SetString(HashKey,JsonUtility.ToJson(new HashList(AssetHashes())));
                Enter("edits-play");
            });
        }

        public static void Recovery()
        {
            Run(() =>
            {
                RequireIsolated();Start("Recovery");
                var game=OpenAuthored();
                var before=FileHashes(true);
                int backups=IceAuthoredRecovery.Backups().Count;
                var active=SceneManager.GetActiveScene();

                // Non-destructive fresh copies.
                string machineCopy=IceAssetPersistence.UniqueFolder(IceAuthoredRecovery.RecoveryFolder,"Machine");
                var fresh=IceMachineBaker.GenerateAssets(game,machineCopy);
                Check(fresh!=null&&fresh.GetComponent<IceMachineRig>()!=null,"fresh machine generated at "+machineCopy);
                string arenaCopy=IceAssetPersistence.UniqueFolder(IceAuthoredRecovery.RecoveryFolder,"Arena");
                Check(IceArenaBaker.Generate(game,arenaCopy,null)!=null,"fresh arena generated at "+arenaCopy);
                Same(before,FileHashes(true),"fresh copies leave every authored file byte-identical");
                Check(!active.isDirty,"fresh copies do not modify or dirty the open scene");
                AssetDatabase.DeleteAsset(IceAuthoredRecovery.RecoveryFolder);

                // Refused confirmation: nothing generated, backed up or written.
                foreach(string typed in new[]{"","rebuild machine","REBUILD"})
                    try { IceAuthoredRecovery.Execute(IceAuthoredRecovery.PlanMachine(false),typed);Check(false,"wrong confirmation must be refused"); }
                    catch(OperationCanceledException) { }
                Same(before,FileHashes(true),"wrong typed confirmations change nothing");
                Check(IceAuthoredRecovery.Backups().Count==backups&&!AssetDatabase.IsValidFolder(IceAuthoredRecovery.RecoveryFolder),"wrong confirmations create no backup or staging folder");
                Check(IceAuthoredRecovery.PlanMachine(false).Explanation().Contains(IceMachineBaker.PrefabPath),"machine explanation names the affected prefab, meshes, materials and scene");

                // Failure part-way through writing rolls everything back.
                IceAuthoredRecovery.FailAfterWrites=1;
                try { IceAuthoredRecovery.Execute(IceAuthoredRecovery.PlanMachine(true),IceAuthoredRecovery.MachinePhrase);Check(false,"injected failure must abort"); }
                catch(InvalidOperationException e) { Check(e.Message.Contains("rolled back"),"injected write failure reported: "+e.Message); }
                finally { IceAuthoredRecovery.FailAfterWrites=-1; }
                Same(before,FileHashes(true),"after a failed replacement every file (and .meta) is byte-identical to before");
                Check(!AssetDatabase.IsValidFolder(IceAuthoredRecovery.RecoveryFolder),"no staging folder left behind");
                game=OpenAuthored();SameFingerprint(game,"after rolled-back replacement");

                // Real replacement keeps GUIDs and the scene link; restore puts every byte back.
                var guids=Guids();
                string backup=IceAuthoredRecovery.Execute(IceAuthoredRecovery.PlanMachine(false),IceAuthoredRecovery.MachinePhrase);
                Check(Directory.Exists(backup)&&File.Exists(Path.Combine(backup,"RESTORE.txt")),"verified backup with restore instructions: "+backup);
                CheckBackup(backup,before);
                Same(guids,Guids(),"prefab, mesh and material GUIDs unchanged by the replacement");
                var after=FileHashes(true);
                Check(after[IceMachineBaker.PrefabPath]!=before[IceMachineBaker.PrefabPath],"machine prefab content was replaced");
                foreach(string mat in Directory.GetFiles(IceMachineBaker.MaterialFolder,"*.mat"))
                    Check(after[mat.Replace('\\','/')]==before[mat.Replace('\\','/')],"materials kept (no reset requested): "+Path.GetFileName(mat));
                game=OpenAuthored();var rig=MachineRig(game);
                Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.gameObject)==IceMachineBaker.PrefabPath,"scene instance still linked to the replaced prefab");
                Check(rig.RearrangedControls().Count==0,"replaced machine is the fresh procedural layout");
                string restore=IceAuthoredRecovery.Execute(IceAuthoredRecovery.PlanRestore(backup),IceAuthoredRecovery.RestorePhrase);
                Same(before,FileHashes(true),"restore from backup returns every machine, arena and scene file byte for byte");
                game=OpenAuthored();SameFingerprint(game,"after machine replace and restore");

                // One arena section: others, materials and the root prefab are untouched.
                string section="Rink boards and ice sheet";
                backup=IceAuthoredRecovery.Execute(IceAuthoredRecovery.PlanArenaSection(section,false),IceAuthoredRecovery.SectionPhrase);
                after=FileHashes(true);
                foreach(var entry in before)
                {
                    bool own=entry.Key.Contains("/Sections/"+IceAssetPersistence.Safe(section)+".");
                    if(!own)Check(after.TryGetValue(entry.Key,out string hash)&&hash==entry.Value,"untouched by the section replacement: "+entry.Key);
                }
                game=OpenAuthored();NoDrift(ArenaRig(game),game,"replaced section still matches the simulated rink");
                IceAuthoredRecovery.Execute(IceAuthoredRecovery.PlanRestore(backup),IceAuthoredRecovery.RestorePhrase);
                Same(before,FileHashes(true),"section restore returns every file byte for byte");
                game=OpenAuthored();SameFingerprint(game,"after recovery tests");
                Pass("COMPLETE");
                Exit(0);
            });
        }

        // ---- Edit mode -------------------------------------------------------------------

        static void CheckEditMode(IceResurfacerGame game,IceArenaRig arena)
        {
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(arena.gameObject)==IceArenaBaker.PrefabPath,"arena is a saved instance of "+IceArenaBaker.PrefabPath);
            Check(arena.transform.parent==game.transform,"arena sits under the game root, beside the machine");
            Check(UnityEngine.Object.FindObjectsByType<IceArenaRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1&&
                  UnityEngine.Object.FindObjectsByType<IceMachineRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"one arena and one machine in the saved scene");
            var sections=arena.Sections();
            Check(sections.Count==IceArenaBaker.Sections.Length,sections.Count+" readable sections");
            for(int i=0;i<sections.Count;i++)
            {
                Check(sections[i].name==IceAssetPersistence.Safe(IceArenaBaker.Sections[i])&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sections[i].gameObject)==
                    IceArenaBaker.SectionPrefabPath(IceArenaBaker.ArenaFolder,IceArenaBaker.Sections[i]),"section is its own prefab: "+sections[i].name);
                Check(sections[i].GetComponentsInChildren<Renderer>(true).Length>0,"section has selectable geometry: "+sections[i].name+" ("+sections[i].GetComponentsInChildren<Renderer>(true).Length+" renderers)");
            }
            int renderers=0,meshes=0,builtIn=0;
            foreach(var renderer in arena.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter!=null&&filter.sharedMesh!=null)
                {
                    Check(EditorUtility.IsPersistent(filter.sharedMesh)&&filter.sharedMesh.isReadable,"saved, readable mesh: "+renderer.name);
                    if(AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(IceArenaBaker.ArenaFolder))meshes++;else builtIn++;
                }
                foreach(var material in renderer.sharedMaterials)Check(material!=null&&EditorUtility.IsPersistent(material),"saved material: "+renderer.name);
            }
            Check(renderers>1000,"Edit Mode shows "+renderers+" individually selectable arena renderers ("+meshes+" generated meshes, "+builtIn+" built-in primitives)");
            foreach(var t in arena.GetComponentsInChildren<Transform>(true))
                Check((t.hideFlags&(HideFlags.DontSaveInEditor|HideFlags.HideInHierarchy))==0,"saved and visible in Hierarchy: "+t.name);
            var lights=arena.GetComponentsInChildren<Light>(true);
            Check(lights.Length==11&&arena.Parts(IceArenaRole.ArenaLight).Count==8,"actual Unity Lights in Edit Mode: "+lights.Length+" (8 arena pool lights, 3 workshop lights)");
            foreach(string name in new[]{"Continuous spatial ice sheet","White impact dasher","Yellow kick strip","Dark top rail","Continuous safety glass","Foundation","Roof canopy",
                "Machine room concrete apron","Dark slots under traversable dump grate","Load-bearing dump grate bar","Machine room side wall","Machine room rear wall",
                "Arena fixture housing","Truss lower chord","Moulded seat bench","Scoreboard body","Hanging club pennant","Aurora board advertisement","Parked regulation goal",
                "Water station backing","Nozzle holder","Service floor drain recess","Wall supply riser","Recessed workshop equipment cabinet"})
                Check(Find(arena.transform,name)!=null,"editable in Edit Mode: "+name);
            Check(arena.GetComponentsInChildren<TextMesh>(true).Length>=20,"signs and labels present ("+arena.GetComponentsInChildren<TextMesh>(true).Length+")");
            NoDrift(arena,game,"fresh arena");
            Check(arena.PhysicalFieldsChangedSinceBake(game.calibration).Count==0,"generated from the scene's current calibration");
            var station=arena.Single(IceArenaRole.WaterStation);
            Check(station.GetComponent<IceKeepUnbatched>()!=null,"water station stays unbatched like the runtime station");
            CheckMaterialsClean("before Play");
        }

        static void NoDrift(IceArenaRig arena,IceResurfacerGame game,string when)
        {
            var findings=IceArenaDrift.Evaluate(arena,game.calibration);
            Check(findings.Count>=20,findings.Count+" physical checks evaluated");
            float worst=0;
            foreach(var finding in findings)
            {
                Check(!finding.Drifted,when+": "+finding.Label+" ("+finding.Detail+")");
                worst=Mathf.Max(worst,finding.Error);
            }
            Pass(when+": largest geometry-vs-simulation difference "+(worst*1000).ToString("0.000")+" mm");
        }

        static void DriftDetection(IceResurfacerGame game,IceArenaRig arena)
        {
            // In-memory only (the scene is reloaded afterwards): physical moves must be reported.
            var cases=new List<(string label,Transform part,Vector3 move,string expect)>
            {
                ("dasher board moved 30 cm",arena.Parts(IceArenaRole.RinkBoardBand)[0].transform,new Vector3(.3f,0,0),"Rink boards: White impact dasher"),
                ("whole rink section moved 20 cm",Find(arena.transform,"Rink boards and ice sheet"),new Vector3(0,0,.2f),"Ice sheet edge on the simulated boards"),
                ("service-bay side wall moved 15 cm",arena.Parts(IceArenaRole.ServiceSideWall)[0].transform,new Vector3(.15f,0,0),"side wall"),
                ("gate jamb pushed into the opening",arena.Parts(IceArenaRole.GateJamb)[0].transform,Vector3.zero,"Service gate opening"),
                ("snow pit grate shifted 25 cm",arena.Single(IceArenaRole.SnowPit),new Vector3(0,0,.25f),"Snow pit boundary"),
                ("water station moved 40 cm",arena.Single(IceArenaRole.WaterStation),new Vector3(.4f,0,0),"Water station"),
            };
            foreach(var c in cases)
            {
                var saved=Local(c.part);
                if(c.move==Vector3.zero)c.part.position-=c.part.parent.TransformDirection(Vector3.right)*Mathf.Sign(c.part.localPosition.x)*.2f;
                else c.part.position+=c.move;
                bool reported=false;string detail="";
                foreach(var finding in IceArenaDrift.Evaluate(arena,game.calibration))
                    if(finding.Drifted&&finding.Label.Contains(c.expect)){reported=true;detail=finding.Label+": "+finding.Detail;}
                Check(reported,"drift reported for "+c.label+" -> "+detail);
                Apply(c.part,saved);
            }
            var config=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
            config.RinkWidth+=.5f;
            bool width=false;foreach(var finding in IceArenaDrift.Evaluate(arena,config))if(finding.Drifted&&finding.Label.StartsWith("Rink width"))width=true;
            Check(width&&arena.PhysicalFieldsChangedSinceBake(config).Contains("RinkWidth"),"calibration change (rink 0.5 m wider) reported against the saved rink");
            // Restore-generated-pose path used by the Inspector button.
            var band=arena.Parts(IceArenaRole.RinkBoardBand)[1];
            band.transform.localPosition+=new Vector3(0,.2f,.4f);
            Check(!band.AtBakedPose,"moved physical part is known to be off its generated pose");
            band.RestoreBakedPose();
            NoDrift(arena,game,"after Restore generated poses");
        }

        static void CheckEquivalence(IceResurfacerGame game,IceArenaRig arena)
        {
            var config=JsonUtility.FromJson<SimulatorConfig>(arena.BakedConfigJson);
            int compared=IceAssetPersistence.InPreviewScene("Validation reference arena",temp =>
            {
                IceWorld reference=null;GameObject host=null;var stationMaterials=new List<Material>();
                try
                {
                    reference=IceWorldBuilder.BuildArenaPreview(temp,game.litTemplate,config);
                    Transform group=null;foreach(Transform child in temp)if(child.name==IceArenaBaker.RootName)group=child;
                    host=new GameObject("reference station"){hideFlags=HideFlags.HideAndDontSave};
                    host.AddComponent<IceWaterService>().BuildFixedStation(game,group,stationMaterials);
                    int count=0;
                    Matrix4x4 toGame=game.transform.worldToLocalMatrix;
                    CompareTree(group,arena.transform,toGame,ref count);
                    var expected=Totals(group);var actual=Totals(arena.transform);
                    foreach(var entry in expected)Check(actual.TryGetValue(entry.Key,out int n)&&n==entry.Value,"same "+entry.Key+" geometry ("+entry.Value+" vertices)");
                    Check(actual.Count==expected.Count,"no extra or missing arena materials ("+actual.Count+")");
                    return count;
                }
                finally
                {
                    if(host!=null)UnityEngine.Object.DestroyImmediate(host);
                    foreach(var material in stationMaterials)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
                    if(reference!=null)
                    {
                        foreach(var mesh in reference.Meshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
                        foreach(var material in reference.Materials)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
                    }
                }
            });
            Pass("arena equivalence: "+compared+" objects match the procedural builder in name, order, pose (<=0.1 mm, 0.01 deg), mesh, material and light settings");
        }
        static void CompareTree(Transform reference,Transform authored,Matrix4x4 toGame,ref int count)
        {
            if(reference.childCount!=authored.childCount)
                throw new InvalidOperationException("Authored arena: "+authored.name+" has "+authored.childCount+" children, builder "+reference.childCount);
            for(int i=0;i<reference.childCount;i++)
            {
                Transform r=reference.GetChild(i),a=authored.GetChild(i);
                if(IceAssetPersistence.Safe(r.name)!=IceAssetPersistence.Safe(a.name))throw new InvalidOperationException("Authored arena: child "+i+" of "+authored.name+" is "+a.name+", builder "+r.name);
                Matrix4x4 rm=r.localToWorldMatrix,am=toGame*a.localToWorldMatrix;
                float dp=Vector3.Distance(rm.GetColumn(3),am.GetColumn(3)),angle=Quaternion.Angle(rm.rotation,am.rotation),scale=(rm.lossyScale-am.lossyScale).magnitude;
                if(dp>.0001f||angle>.01f||scale>.0001f)throw new InvalidOperationException("Authored arena: "+a.name+" moved from the builder pose (dp="+dp+", angle="+angle+", scale="+scale+")");
                var rf=r.GetComponent<MeshFilter>();var af=a.GetComponent<MeshFilter>();
                if((rf==null)!=(af==null)||(rf!=null&&(rf.sharedMesh==null)!=(af.sharedMesh==null))||(rf!=null&&rf.sharedMesh!=null&&rf.sharedMesh.vertexCount!=af.sharedMesh.vertexCount))
                    throw new InvalidOperationException("Authored arena: mesh differs on "+a.name);
                var rr=r.GetComponent<MeshRenderer>();var ar=a.GetComponent<MeshRenderer>();
                if(rr!=null&&rr.GetComponent<TextMesh>()==null)SameMaterial(rr,ar);
                var rt=r.GetComponent<TextMesh>();var at=a.GetComponent<TextMesh>();
                if(rt!=null&&(at==null||rt.text!=at.text||rt.characterSize!=at.characterSize||rt.color!=at.color))throw new InvalidOperationException("Authored arena: label differs: "+a.name);
                var rl=r.GetComponent<Light>();var al=a.GetComponent<Light>();
                if(rl!=null&&(al==null||rl.type!=al.type||rl.intensity!=al.intensity||rl.range!=al.range||rl.color!=al.color||rl.shadows!=al.shadows))
                    throw new InvalidOperationException("Authored arena: light differs: "+a.name);
                if(r.gameObject.activeSelf!=a.gameObject.activeSelf)throw new InvalidOperationException("Authored arena: active state differs: "+a.name);
                count++;
                CompareTree(r,a,toGame,ref count);
            }
        }
        static readonly HashSet<string> checkedMaterials=new HashSet<string>();
        static void SameMaterial(MeshRenderer reference,MeshRenderer authored)
        {
            var source=reference.sharedMaterial;var asset=authored.sharedMaterial;
            string label=IceAssetPersistence.Safe(IceAssetPersistence.LabelOf(source));
            if(asset==null||asset.name!=label)throw new InvalidOperationException("Authored arena: "+authored.name+" uses "+(asset!=null?asset.name:"no material")+", builder "+label);
            if(!checkedMaterials.Add(label))return;
            var a=new HashSet<string>(asset.shaderKeywords);var r=new HashSet<string>(source.shaderKeywords);
            bool same=a.SetEquals(r)&&asset.shader==source.shader&&asset.renderQueue==source.renderQueue&&asset.GetColor("_BaseColor")==source.GetColor("_BaseColor")&&
                asset.GetColor("_EmissionColor")==source.GetColor("_EmissionColor")&&asset.GetFloat("_Metallic")==source.GetFloat("_Metallic")&&asset.GetFloat("_Smoothness")==source.GetFloat("_Smoothness");
            foreach(string property in new[]{"_Surface","_Blend","_ZWrite","_SrcBlend","_DstBlend","_Cull"})same&=asset.GetFloat(property)==source.GetFloat(property);
            Check(same,"persisted material renders like the builder's: "+label+" [keywords "+string.Join(",",a)+" vs "+string.Join(",",r)+"; queue "+asset.renderQueue+"]");
        }
        static Dictionary<string,int> Totals(Transform root)
        {
            var totals=new Dictionary<string,int>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(filter.sharedMesh==null||renderer==null||renderer.sharedMaterial==null)continue;
                string key=IceAssetPersistence.Safe(IceAssetPersistence.LabelOf(renderer.sharedMaterial));
                totals.TryGetValue(key,out int count);totals[key]=count+filter.sharedMesh.vertexCount;
            }
            return totals;
        }

        // ---- Play Mode -------------------------------------------------------------------

        static void Tick()
        {
            string stage=SessionState.GetString(StageKey,"");
            try
            {
                if(stage.EndsWith("-play"))
                {
                    if(!EditorApplication.isPlaying)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.Sim==null) { if(++frame>400)throw new InvalidOperationException("Authored scene did not initialize in Play Mode");return; }
                    if(++frame<45)return;
                    if(stage=="verify-play")VerifyPlay(game);else VerifyEditsPlay(game);
                    SessionState.SetString(StageKey,stage.Replace("-play","-exit"));
                    EditorApplication.ExitPlaymode();
                }
                else if(stage.EndsWith("-exit"))
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    CheckAssetsAfterPlay();
                    if(stage=="edits-exit")RestoreEdits();
                    var game=OpenAuthored();SameFingerprint(game,"after Play");
                    SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                    Pass("COMPLETE");Exit(0);
                }
            }
            catch(Exception e) { Fail(e); }
        }

        static void VerifyPlay(IceResurfacerGame game)
        {
            var arena=ArenaRig(game);var w=game.World;
            Check(w.IceMesh==arena.Single(IceArenaRole.IceSheet).GetComponent<MeshRenderer>(),"Play binds the saved ice sheet (no regenerated rink)");
            Check(w.PitSnow==arena.Single(IceArenaRole.PitSnow),"Play binds the saved pit snow surface");
            var lights=arena.Parts(IceArenaRole.ArenaLight);
            Check(w.ArenaLights.Length==8&&lights.Count==8,"Play binds the 8 saved arena pool lights");
            for(int i=0;i<8;i++)Check(w.ArenaLights[i]==lights[i].GetComponent<Light>()&&w.ArenaLights[i].enabled,"arena light "+i+" is the authored Light");
            Check(w.Machine==MachineRig(game).Collect().Machine,"Play still binds the saved machine");
            foreach(var entry in new Dictionary<string,int>{{"NorthlineArena",1},{IceArenaBaker.RootName,0},{"Continuous spatial ice sheet",1},{"Foundation",1},{"Roof canopy",1},{"Centre suspended scoreboard",1},
                {"Tiered spectator seating",1},{"Southeast diagonal portal - backstage maintenance room",1},{IceAssetPersistence.Safe(IceArenaBaker.StationSection),1},{"Water station backing",1},
                {"Arena pool light",8},{"Workshop pool light",3},{"Parked regulation goal",2},{"AURORA R-54 / rear axle origin",1},{"Trailing conditioner / articulated hitch",1}})
                Check(Count(entry.Key)==entry.Value,"no duplicate in Play: "+entry.Key+" x"+Count(entry.Key));
            Check(arena.GetComponentsInChildren<Light>(true).Length==11,"11 arena Lights in Play (8 pool, 3 workshop), none generated twice");
            var ice=w.IceMesh.sharedMaterial;
            Check(ice!=null&&!EditorUtility.IsPersistent(ice)&&ice.shader==game.iceTemplate.shader,"ice sheet renders the live spatial ice material ("+(ice!=null?ice.name:"none")+"), not an asset");
            Check(ice.GetTexture("_StateTex")!=null&&ice.GetTexture("_ProcessTex")!=null&&ice.GetTexture("_DepthTex")!=null,"ice field, process and water-depth textures bound to the saved sheet");
            int batched=0;foreach(Transform child in arena.transform)if(child.name.StartsWith("Batched "))batched++;
            int live=0;foreach(var r in arena.GetComponentsInChildren<MeshRenderer>())if(r.enabled)live++;
            Check(batched>5,"runtime material batching ran on the Play instance ("+batched+" arena batches, "+live+" live arena renderers)");
            Check(arena.Single(IceArenaRole.WaterStation).GetComponentsInChildren<MeshRenderer>().Length>20,"water station kept as separate parts (unbatched, as before)");
            foreach(var mesh in w.Meshes)Check(mesh==null||!EditorUtility.IsPersistent(mesh),"runtime destroy list holds no persistent mesh: "+(mesh!=null?mesh.name:""));
            foreach(var material in w.Materials)Check(material==null||!EditorUtility.IsPersistent(material),"runtime destroy list holds no persistent material: "+(material!=null?material.name:""));
            foreach(var text in arena.GetComponentsInChildren<TextMesh>(true))
                Check(!EditorUtility.IsPersistent(text.GetComponent<MeshRenderer>().sharedMaterial),"label uses a runtime depth-tested material: "+text.text.Replace("\n"," "));
            var service=game.WaterService;var station=arena.Single(IceArenaRole.WaterStation);
            Check(service.SupplyHandle==arena.Single(IceArenaRole.SupplyValveHandle),"water service drives the saved supply valve handle");
            Check(Vector3.Distance(service.HolderPosition,station.TransformPoint(5.40f,1.15f,-6.48f))<1e-4f&&Vector3.Distance(service.SupplyPosition,station.TransformPoint(5.38f,1.25f,-5.28f))<1e-4f&&
                Vector3.Distance(service.FloorDrainPosition,station.TransformPoint(4.6f,.025f,-7.35f))<1e-4f,"hose holder, supply valve and drain interaction points sit on the saved station");
            var layout=new ServiceLayout(game.calibration);
            layout.HoseHolder(out float hx,out float hz);layout.SupplyAnchor(out float ax,out float az);
            Check(Vector2.Distance(new Vector2(service.HolderPosition.x,service.HolderPosition.z),new Vector2(hx,hz))<.75f,"saved hose holder beside the simulated holder point");
            Check(Count(IceAssetPersistence.Safe(IceArenaBaker.StationSection))==1&&Count("Service nozzle / carried and coupled")==1,"one station and one runtime nozzle");
            Check(game.GetComponentsInChildren<AudioSource>().Length==27,"27 audio sources, as in the launch scene");
            Check(arena.BoundFindings!=null&&arena.BoundFindings.Count>=20,"Play: geometry report taken when the arena was bound ("+arena.BoundFindings.Count+" checks)");
            foreach(var finding in arena.BoundFindings)Check(!finding.Drifted,"Play: "+finding.Label+" agrees with collision");
        }

        static void VerifyEditsPlay(IceResurfacerGame game)
        {
            var record=EditRecord();var arena=ArenaRig(game);var w=game.World;
            Light moved=null;
            foreach(var light in w.ArenaLights)if(Vector3.Distance(GameSpace(game,light.transform.position),record.lightWorld)<1e-4f)moved=light;
            Check(moved!=null&&Mathf.Approximately(moved.intensity,record.lightIntensity)&&moved.color==record.lightColor,"Play: moved, retuned ceiling Light is a bound arena light");
            Check(Batched(arena,record.advertWorld,"Signal vermilion"),"Play: moved board advertisement drawn at its edited place with its new material");
            Check(Batched(arena,record.housingWorld,"Instrument graphite"),"Play: moved fixture housing drawn at its edited place");
            foreach(var finding in arena.BoundFindings)Check(!finding.Drifted,"Play after cosmetic edits: "+finding.Label);
            Check(Count("Arena pool light")==8&&Count("NorthlineArena")==1&&Count(IceArenaBaker.RootName)==0,"edited arena still not duplicated in Play");
        }
        static bool Batched(IceArenaRig arena,Vector3 centre,string material)
        {
            foreach(Transform child in arena.transform)
            {
                if(!child.name.StartsWith("Batched ")||!child.name.Contains(material))continue;
                var mesh=child.GetComponent<MeshFilter>().sharedMesh;var m=arena.transform.parent.worldToLocalMatrix*child.localToWorldMatrix;
                var bounds=new Bounds();bool any=false;
                foreach(var v in mesh.vertices)
                {
                    Vector3 p=m.MultiplyPoint3x4(v);
                    if(Vector3.Distance(p,centre)>3.5f)continue;
                    if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);
                }
                if(any&&Vector3.Distance(bounds.center,centre)<.05f)return true;
            }
            return false;
        }

        // ---- Assets ----------------------------------------------------------------------

        [Serializable] sealed class HashList { public List<string> entries=new List<string>(); public HashList(){} public HashList(List<string> e){entries=e;} }
        static List<string> AssetHashes()
        {
            var list=new List<string>();
            foreach(var entry in FileHashes(false))list.Add(entry.Key+" "+entry.Value);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
        // Machine, arena and authored-scene files (with .meta when requested), SHA-256 by path.
        static Dictionary<string,string> FileHashes(bool metas)
        {
            var hashes=new Dictionary<string,string>();
            var files=new List<string>{IceMachineBaker.AuthoredScenePath,IceMachineBaker.AuthoredScenePath+".meta"};
            foreach(string folder in new[]{IceMachineBaker.MachineFolder,IceArenaBaker.ArenaFolder})
                if(Directory.Exists(folder))files.AddRange(Directory.GetFiles(folder,"*",SearchOption.AllDirectories));
            foreach(string file in files)
            {
                if(!metas&&file.EndsWith(".meta"))continue;
                if(File.Exists(file))hashes[file.Replace('\\','/')]=Hash(File.ReadAllBytes(file));
            }
            return hashes;
        }
        static void Same(Dictionary<string,string> before,Dictionary<string,string> after,string message)
        {
            var diff=new List<string>();
            foreach(var entry in before)if(!after.TryGetValue(entry.Key,out string hash)||hash!=entry.Value)diff.Add(entry.Key);
            foreach(var entry in after)if(!before.ContainsKey(entry.Key))diff.Add("+"+entry.Key);
            Check(diff.Count==0,message+" ("+before.Count+" files)"+(diff.Count>0?": "+string.Join(", ",diff):""));
        }
        static void CheckBackup(string backup,Dictionary<string,string> before)
        {
            int files=0;
            foreach(var entry in before)
            {
                string copy=IceAuthoredRecovery.BackupCopyPath(backup,entry.Key);
                if(!File.Exists(copy))continue;
                Check(Hash(File.ReadAllBytes(copy))==entry.Value,"backup copy is byte-identical: "+entry.Key);files++;
            }
            Check(files>=4,files+" files and .meta files in the backup");
        }
        static Dictionary<string,string> Guids()
        {
            var guids=new Dictionary<string,string>();
            foreach(string file in Directory.GetFiles(IceMachineBaker.MachineFolder,"*",SearchOption.AllDirectories))
                if(!file.EndsWith(".meta"))guids[file.Replace('\\','/')]=AssetDatabase.AssetPathToGUID(file.Replace('\\','/'));
            return guids;
        }
        static void CheckAssetsAfterPlay()
        {
            var before=JsonUtility.FromJson<HashList>(SessionState.GetString(HashKey,"{}")).entries;
            var after=AssetHashes();
            Check(before.Count>0&&before.Count==after.Count,"same "+after.Count+" machine, arena and scene files after Play");
            for(int i=0;i<after.Count;i++)Check(before[i]==after[i],"unchanged by Play: "+after[i].Split(' ')[0]);
            foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{IceArenaBaker.ArenaFolder,IceMachineBaker.MachineFolder}))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(path));
                Check(prefab!=null&&!EditorUtility.IsDirty(prefab),"prefab intact and clean after Play: "+AssetDatabase.GUIDToAssetPath(path));
            }
            CheckMaterialsClean("after Play");
            var scene=SceneManager.GetActiveScene();
            Check(scene.path==IceMachineBaker.AuthoredScenePath&&!scene.isDirty,"authored scene not modified by Play");
            var arena=ArenaRig(Game());
            foreach(var filter in arena.GetComponentsInChildren<MeshFilter>(true))
                if(filter.GetComponent<IceArenaPart>()==null||filter.GetComponent<IceArenaPart>().role!=IceArenaRole.PitSnow)
                    Check(filter.sharedMesh!=null&&EditorUtility.IsPersistent(filter.sharedMesh),"Edit Mode mesh reference intact after Play: "+filter.name);
            Check(Find(arena.transform,"Continuous spatial ice sheet").GetComponent<MeshRenderer>().sharedMaterial!=null&&
                EditorUtility.IsPersistent(Find(arena.transform,"Continuous spatial ice sheet").GetComponent<MeshRenderer>().sharedMaterial),"ice sheet back on its saved material after Play");
        }
        // Play must not dirty a material asset. One already dirty in this process before Play (URP's
        // import validation after a scripted colour edit) is recorded, reported and excluded.
        const string DirtyKey="Northline.AuthoredArena.Dirty";
        static void CheckMaterialsClean(string when)
        {
            bool before=when=="before Play";
            var earlier=new HashSet<string>(SessionState.GetString(DirtyKey,"").Split('|'));
            var dirty=new List<string>();
            foreach(string folder in new[]{IceArenaBaker.MaterialFolder,IceMachineBaker.MaterialFolder})
                foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{folder}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    Check(material!=null,"material asset loads "+when+": "+path);
                    if(!EditorUtility.IsDirty(material)){Pass("material asset clean "+when+": "+material.name);continue;}
                    dirty.Add(path);
                    if(before)Info("material already dirty in memory before Play (file unchanged): "+path);
                    else Check(earlier.Contains(path),"Play did not dirty material asset: "+path);
                }
            if(before)SessionState.SetString(DirtyKey,string.Join("|",dirty));
        }
        static void RestoreEdits()
        {
            var record=EditRecord();
            string banners=IceArenaBaker.SectionPrefabPath(IceArenaBaker.ArenaFolder,BannerSection);
            var root=PrefabUtility.LoadPrefabContents(banners);
            try
            {
                var advert=Named(root.transform,"Aurora board advertisement");
                Apply(advert,record.advertLocal);advert.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(record.advertMaterialOriginal);
                PrefabUtility.SaveAsPrefabAsset(root,banners);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            string lights=IceArenaBaker.SectionPrefabPath(IceArenaBaker.ArenaFolder,LightSection);
            root=PrefabUtility.LoadPrefabContents(lights);
            try
            {
                Apply(Moved(root.transform,"Arena fixture housing",record.housingLocal),record.housingLocal);
                Apply(Moved(root.transform,"Arena fixture diffuser",record.diffuserLocal),record.diffuserLocal);
                var light=Moved(root.transform,"Arena pool light",record.lightLocal);
                Apply(light,record.lightLocal);light.GetComponent<Light>().intensity=record.lightIntensityOriginal;light.GetComponent<Light>().color=record.lightColorOriginal;
                PrefabUtility.SaveAsPrefabAsset(root,lights);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var seat=AssetDatabase.LoadAssetAtPath<Material>(IceArenaBaker.MaterialFolder+"/Grandstand blue.mat");
            seat.SetColor("_BaseColor",record.seatOriginal);EditorUtility.SetDirty(seat);AssetDatabase.SaveAssets();
            var game=OpenAuthored();NoDrift(ArenaRig(game),game,"restored arena edits");
            CheckEquivalence(game,ArenaRig(game));
            Pass("arena edits restored to the generated arena");
        }

        // ---- Machine fingerprint (proves the user's machine is untouched) -----------------

        static string MachineFingerprint(IceResurfacerGame game)
        {
            var text=new StringBuilder();
            foreach(var entry in Sorted(FileHashesIn(IceMachineBaker.MachineFolder)))text.AppendLine("file "+entry);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(IceMachineBaker.PrefabPath);
            Describe(prefab.transform,prefab.transform,text,"prefab ");
            foreach(string file in Directory.GetFiles(IceMachineBaker.MaterialFolder,"*.mat"))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\','/'));
                var keywords=new List<string>(material.shaderKeywords);keywords.Sort(StringComparer.Ordinal);
                text.Append("material ").Append(material.name).Append(" shader=").Append(material.shader.name).Append(" queue=").Append(material.renderQueue)
                    .Append(" gi=").Append(material.globalIlluminationFlags).Append(" keywords=").Append(string.Join(",",keywords));
                for(int i=0;i<material.shader.GetPropertyCount();i++)
                {
                    string name=material.shader.GetPropertyName(i);
                    switch(material.shader.GetPropertyType(i))
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Color: text.Append(' ').Append(name).Append('=').Append(material.GetColor(name).ToString("F6"));break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector: text.Append(' ').Append(name).Append('=').Append(material.GetVector(name).ToString("F6"));break;
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                        case UnityEngine.Rendering.ShaderPropertyType.Range: text.Append(' ').Append(name).Append('=').Append(material.GetFloat(name).ToString("R"));break;
                        case UnityEngine.Rendering.ShaderPropertyType.Texture: text.Append(' ').Append(name).Append('=').Append(material.GetTexture(name)!=null?AssetDatabase.GetAssetPath(material.GetTexture(name)):"none");break;
                    }
                }
                text.AppendLine();
            }
            var rig=MachineRig(game);
            var mods=new List<string>();
            foreach(var mod in PrefabUtility.GetPropertyModifications(rig.gameObject)??new PropertyModification[0])
                mods.Add((mod.target!=null?mod.target.name+"/"+mod.target.GetType().Name:"null")+" "+mod.propertyPath+"="+mod.value+" ref="+(mod.objectReference!=null?mod.objectReference.name:""));
            mods.Sort(StringComparer.Ordinal);
            foreach(string mod in mods)text.AppendLine("scene override "+mod);
            foreach(var added in PrefabUtility.GetAddedGameObjects(rig.gameObject))text.AppendLine("scene added "+added.instanceGameObject.name);
            foreach(var removed in PrefabUtility.GetRemovedComponents(rig.gameObject))text.AppendLine("scene removed component "+removed.assetComponent.name);
            text.AppendLine("scene parent "+(rig.transform.parent!=null?rig.transform.parent.name:"none")+" sibling "+rig.transform.GetSiblingIndex());
            Describe(rig.transform,game.transform,text,"scene ");
            return text.ToString();
        }
        static void Describe(Transform root,Transform frame,StringBuilder text,string prefix)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                Matrix4x4 m=frame.worldToLocalMatrix*t.localToWorldMatrix;
                text.Append(prefix).Append(PathOf(t,root)).Append(" active=").Append(t.gameObject.activeSelf)
                    .Append(" local=").Append(t.localPosition.ToString("F6")).Append(t.localRotation.ToString("F6")).Append(t.localScale.ToString("F6"))
                    .Append(" frame=").Append(((Vector3)m.GetColumn(3)).ToString("F6"));
                foreach(var component in t.GetComponents<Component>())
                {
                    if(component==null){text.Append(" [missing script]");continue;}
                    text.Append(" [").Append(component.GetType().Name);
                    if(component is MeshFilter filter)text.Append(" ").Append(filter.sharedMesh!=null?AssetDatabase.GetAssetPath(filter.sharedMesh)+":"+filter.sharedMesh.name:"none");
                    if(component is Renderer renderer)
                    {
                        text.Append(" shadows=").Append(renderer.shadowCastingMode).Append(" enabled=").Append(renderer.enabled);
                        foreach(var material in renderer.sharedMaterials)text.Append(" ").Append(material!=null?AssetDatabase.GetAssetPath(material)+":"+material.name:"none");
                    }
                    if(component is TextMesh label)text.Append(" '").Append(label.text.Replace("\n","|")).Append("'");
                    if(component is Behaviour behaviour)text.Append(" enabled=").Append(behaviour.enabled);
                    text.Append("]");
                }
                text.AppendLine();
            }
        }
        static void SameFingerprint(IceResurfacerGame game,string when)
        {
            string baseline=File.ReadAllText(FingerprintPath),now=MachineFingerprint(game);
            if(baseline!=now)
            {
                File.WriteAllText(Output+"machine-fingerprint-"+IceAssetPersistence.Safe(when)+".txt",now);
                var a=baseline.Split('\n');var b=now.Split('\n');int line=0;
                while(line<a.Length&&line<b.Length&&a[line]==b[line])line++;
                Check(false,"authored machine changed "+when+" at line "+(line+1)+":\n  was: "+(line<a.Length?a[line]:"")+"\n  now: "+(line<b.Length?b[line]:""));
            }
            Pass("authored machine identical to the baseline "+when+" (prefab, meshes, materials, scene instance and overrides; "+baseline.Split('\n').Length+" lines)");
        }
        // What the user changed relative to a fresh generation, for the report (not asserted).
        static void UserEdits(IceResurfacerGame game,IceMachineRig rig)
        {
            var moved=rig.RearrangedControls();
            Info("operator controls moved since the bake: "+(moved.Count==0?"none":string.Join(", ",moved)));
            var w=rig.Collect();
            foreach(var anchor in IceMachineRig.MechanicalAnchors(w,game.calibration))
                Info("physics anchor "+anchor.Label+": "+(Vector3.Distance(anchor.Actual,anchor.Expected)*1000).ToString("0.0")+" mm from config");
            var mods=PrefabUtility.GetPropertyModifications(rig.gameObject);
            Info("scene overrides on the machine instance: "+(mods!=null?mods.Length:0));
            string fresh=IceAssetPersistence.UniqueFolder("Assets/Experiments/IceResurfacer/Recovery","Baseline comparison");
            var prefab=IceMachineBaker.GenerateAssets(game,fresh);
            try
            {
                var a=new Dictionary<string,Transform>();var b=new Dictionary<string,Transform>();
                foreach(var t in prefab.GetComponentsInChildren<Transform>(true))a[PathOf(t,prefab.transform)]=t;
                var authoredPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(IceMachineBaker.PrefabPath);
                foreach(var t in authoredPrefab.GetComponentsInChildren<Transform>(true))b[PathOf(t,authoredPrefab.transform)]=t;
                int changed=0;var samples=new List<string>();
                foreach(var entry in b)
                {
                    if(!a.TryGetValue(entry.Key,out var t)){changed++;if(samples.Count<12)samples.Add("added "+entry.Key);continue;}
                    if(Vector3.Distance(t.localPosition,entry.Value.localPosition)>.0005f||Quaternion.Angle(t.localRotation,entry.Value.localRotation)>.05f||Vector3.Distance(t.localScale,entry.Value.localScale)>.0005f)
                    {changed++;if(samples.Count<12)samples.Add("moved "+entry.Key+" by "+(Vector3.Distance(t.localPosition,entry.Value.localPosition)*1000).ToString("0")+" mm");}
                }
                foreach(var entry in a)if(!b.ContainsKey(entry.Key)){changed++;if(samples.Count<12)samples.Add("removed "+entry.Key);}
                Info("authored prefab differs from a fresh generation in "+changed+" transforms"+(samples.Count>0?": "+string.Join("; ",samples):""));
                foreach(string file in Directory.GetFiles(IceMachineBaker.MaterialFolder,"*.mat"))
                {
                    var mine=AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\','/'));
                    var theirs=AssetDatabase.LoadAssetAtPath<Material>(fresh+"/Materials/"+Path.GetFileName(file));
                    if(theirs!=null&&mine.GetColor("_BaseColor")!=theirs.GetColor("_BaseColor"))Info("material recoloured: "+mine.name+" "+mine.GetColor("_BaseColor"));
                }
            }
            finally { AssetDatabase.DeleteAsset("Assets/Experiments/IceResurfacer/Recovery"); }
        }

        // ---- Screenshots -----------------------------------------------------------------

        static void Capture(string label)
        {
            var layout=new ServiceLayout(Game().calibration);
            Vector3 W(float side,float y,float outward){layout.ToWorld(side,outward,out float x,out float z);return new Vector3(x,y,z);}
            var views=new List<(string,Vector3,Vector3)>
            {
                ("Overview",new Vector3(-11,8.6f,-27),new Vector3(4,0,8)),
                ("Rink-and-boards",new Vector3(-9,1.7f,-24),new Vector3(10,.8f,-26)),
                ("Service-bay",W(-4.6f,3.6f,3.2f),W(.5f,.4f,8.8f)),
                ("Water-station",W(-1.5f,1.8f,4.4f),W(-5.3f,1.2f,5.8f)),
                ("Machine",new Vector3(4,2.6f,6),new Vector3(0,1,.5f)),
                ("Roof-and-lights",new Vector3(0,5,-30),new Vector3(0,9.6f,0)),
            };
            Directory.CreateDirectory(Output);
            var node=new GameObject("Validation capture camera"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var camera=node.AddComponent<Camera>();camera.fieldOfView=60;camera.nearClipPlane=.05f;camera.farClipPlane=200;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.13f,.18f);
                foreach(var view in views)
                {
                    node.transform.position=view.Item2;node.transform.LookAt(view.Item3);
                    IceResurfacerEditor.Capture(camera,"AuthoredArena/"+label+"-"+view.Item1+".png",1600,900);
                }
                Pass("Edit Mode screenshots ("+label+"): "+views.Count+" views in Validation~/AuthoredArena/");
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        // ---- Helpers ---------------------------------------------------------------------

        static Pose Local(Transform t) { return new Pose { position=t.localPosition, rotation=t.localRotation, scale=t.localScale }; }
        static void Apply(Transform t,Pose p) { t.localPosition=p.position;t.localRotation=p.rotation;t.localScale=p.scale; }
        static Vector3 GameSpace(IceResurfacerGame game,Vector3 world) { return game.transform.InverseTransformPoint(world); }
        static void Near(Vector3 actual,Vector3 expected,string message)
        { Check(Vector3.Distance(actual,expected)<.0001f,message+" (error "+(Vector3.Distance(actual,expected)*1000).ToString("F3")+" mm)"); }
        static Edits EditRecord()
        {
            if(!File.Exists(EditsPath))throw new InvalidOperationException("Run WriteEdits first.");
            return JsonUtility.FromJson<Edits>(File.ReadAllText(EditsPath));
        }
        static Transform Named(Transform root,string name)
        {
            var t=Find(root,name);
            if(t==null)throw new InvalidOperationException("Missing "+name);
            return t;
        }
        // The part with this name that was moved off its original local pose (siblings sit metres away).
        static Transform Moved(Transform root,string name,Pose original)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(t.name==name&&Vector3.Distance(t.localPosition,original.position)>.01f&&Vector3.Distance(t.localPosition,original.position)<2)return t;
            throw new InvalidOperationException("No moved "+name);
        }
        static Transform Find(Transform root,string name)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
            return null;
        }
        static string PathOf(Transform t,Transform root)
        {
            var parts=new List<string>();
            for(var p=t;p!=null&&p!=root;p=p.parent)parts.Add(p.name+"#"+p.GetSiblingIndex());
            parts.Reverse();
            return string.Join("/",parts);
        }
        static int Count(string name)
        {
            int n=0;
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t.name==name)n++;
            return n;
        }
        static IEnumerable<string> Sorted(Dictionary<string,string> hashes)
        {
            var list=new List<string>();foreach(var entry in hashes)list.Add(entry.Key+" "+entry.Value);
            list.Sort(StringComparer.Ordinal);return list;
        }
        static Dictionary<string,string> FileHashesIn(string folder)
        {
            var hashes=new Dictionary<string,string>();
            foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))hashes[file.Replace('\\','/')]=Hash(File.ReadAllBytes(file));
            return hashes;
        }
        static string Hash(byte[] bytes)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
        }
        static IceResurfacerGame OpenAuthored()
        {
            EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
            return Game();
        }
        static IceResurfacerGame Game()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
            if(game==null)throw new InvalidOperationException("No IceResurfacerGame in "+SceneManager.GetActiveScene().path);
            return game;
        }
        static IceMachineRig MachineRig(IceResurfacerGame game)
        {
            var rig=game.GetComponentInChildren<IceMachineRig>(true);
            if(rig==null)throw new InvalidOperationException("No authored machine under the game root.");
            return rig;
        }
        static IceArenaRig ArenaRig(IceResurfacerGame game)
        {
            var rig=game.GetComponentInChildren<IceArenaRig>(true);
            if(rig==null)throw new InvalidOperationException("No authored arena under the game root; run SetUp first.");
            return rig;
        }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Authored-arena validation writes assets; run it only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Enter(string stage)
        {
            frame=0;SessionState.SetString(StageKey,stage);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorApplication.EnterPlaymode();
        }
        static void Start(string step) { Directory.CreateDirectory(Root+"Validation~");File.AppendAllText(ResultsPath,"== "+step+" / "+DateTime.Now.ToString("s")+"\n"); }
        static void Pass(string message) { File.AppendAllText(ResultsPath,"PASS "+message+"\n"); }
        static void Info(string message) { File.AppendAllText(ResultsPath,"INFO "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Authored arena: "+message);
            Pass(message);
        }
        static void Run(Action body)
        {
            try { body(); }
            catch(Exception e) { Fail(e); }
        }
        static void Fail(Exception e)
        {
            Debug.LogException(e);
            try { File.AppendAllText(ResultsPath,"FAIL "+e.Message+"\n"); } catch { }
            SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
            Exit(1);
        }
        static void Exit(int code) { Debug.Log("NORTHLINE AUTHORED ARENA VALIDATION exit="+code);EditorApplication.Exit(code); }
    }
}
