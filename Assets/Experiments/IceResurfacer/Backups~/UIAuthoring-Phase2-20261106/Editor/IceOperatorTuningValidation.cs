using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    // Explicit read-only scene checks in Validation~/UnityProject. No baking or asset saves.
    [InitializeOnLoad]
    public static class IceOperatorTuningValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string Out=Root+"Validation~/OperatorTuning/";
        const string Stage="Aurora.OperatorTuning.ValidationStage";
        static int frames;
        static IceOperatorTuningValidation(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Log(string text){Directory.CreateDirectory(Out);File.AppendAllText(Out+"unity-results.txt",text+"\n");Debug.Log(text);}
        static void Check(bool value,string text){OperatorMaterialValidation.Require(value,text);Log("PASS "+text);}
        static string Hash(string path)
        {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
        static SimulatorConfig Copy(SimulatorConfig c)=>JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(c));
        static IceResurfacerGame Game()=>UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
        public static void Run()
        {
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated validation project.");
                Directory.CreateDirectory(Out);File.WriteAllText(Out+"unity-results.txt","");
                SessionState.SetString(Stage+"Prefab",Hash(Root+"Machine/AuroraR54.prefab"));
                SessionState.SetString(Stage+"Scene",Hash(Root+"Scenes/IceResurfacer_AuthoredMachine.unity"));
                SessionState.SetString(Stage+"Launch",Hash(Root+"Scenes/IceResurfacer.unity"));
                EditorSceneManager.OpenScene(Root+"Scenes/IceResurfacer_AuthoredMachine.unity");
                var game=Game();Audit(game);
                Check(game.GetComponent<IceOperatorTuning>()!=null,"authored scene has a saved Operator Tuning component");
                var baseline=Copy(game.calibration);var tuned=game.GetComponent<IceOperatorTuning>().CreateRuntimeConfig(game.calibration);
                OperatorMaterialValidation.WashCut(baseline,Log,1,true);
                float wash=OperatorMaterialValidation.WashCut(tuned,Log,1,true);
                Check(Mathf.Abs(wash-248)<2,"ten-minute wash target within 2 L at authored full flood 0.72 L/s");
                OperatorMaterialValidation.WashCut(Copy(tuned),Log,.5f,true);
                OperatorMaterialValidation.OrdinaryStreak(baseline,Log,false);
                OperatorMaterialValidation.OrdinaryStreak(tuned,Log,true);
                OperatorMaterialValidation.FullSheet(Copy(tuned),Log,true);
                OperatorMaterialValidation.FullSheet(Copy(tuned),Log,false);
                var expanded=Copy(tuned);expanded.SnowExpansionRatio*=2.5f;
                OperatorMaterialValidation.FullSheet(expanded,Log,true);
                SessionState.SetInt(Stage,1);frames=0;EditorApplication.EnterPlaymode();
            }
            catch(Exception e){Fail(e);}
        }
        static void State(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode||SessionState.GetInt(Stage,0)!=2)return;
            EditorApplication.delayCall+=()=>
            {
                try{EditorSceneManager.OpenScene(Root+"Scenes/IceResurfacer.unity");SessionState.SetInt(Stage,3);frames=0;EditorApplication.EnterPlaymode();}
                catch(Exception e){Fail(e);}
            };
        }
        static void Tick()
        {
            int stage=SessionState.GetInt(Stage,0);
            if(!EditorApplication.isPlaying||(stage!=1&&stage!=3)||++frames<8)return;
            try
            {
                var game=Game();Check(game!=null&&game.Sim!=null,"scene enters Play Mode with initialized simulation");game.enabled=false;
                if(stage==1)
                {
                    AuthoredPlay(game);SessionState.SetInt(Stage,2);EditorApplication.ExitPlaymode();
                }
                else
                {
                    Check(game.GetComponent<IceOperatorTuning>()==null,"procedural scene has no tuning component");
                    Check(game.Sim.Config.FloodMaxLitersPerSecond==1.8f&&game.Sim.Config.WashRecoveryFraction==.82f&&game.Sim.Config.LooseSnowPickupDepthMm==6,"procedural launch retains original material defaults");
                    OperatorMaterialValidation.FullSheet(Copy(game.Sim.Config),Log,true);
                    DrivenCut(Copy(game.Sim.Config),"procedural");
                    Check(Hash(Root+"Machine/AuroraR54.prefab")==SessionState.GetString(Stage+"Prefab",""),"machine prefab byte-identical after validation");
                    Check(Hash(Root+"Scenes/IceResurfacer_AuthoredMachine.unity")==SessionState.GetString(Stage+"Scene",""),"authored scene not saved or modified by validation");
                    Check(Hash(Root+"Scenes/IceResurfacer.unity")==SessionState.GetString(Stage+"Launch",""),"procedural launch scene byte-identical");
                    SessionState.SetInt(Stage,0);Log("ALL OPERATOR TUNING UNITY CHECKS PASSED");EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){SessionState.SetInt(Stage,0);Log("FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
        static void AuthoredPlay(IceResurfacerGame game)
        {
            var t=game.GetComponent<IceOperatorTuning>();var saved=JsonUtility.ToJson(game.calibration);
            game.NewSession(true);var s=game.Sim;
            Check(!ReferenceEquals(s.Config,game.calibration),"runtime tuning uses a private configuration copy");
            Check(Mathf.Abs(s.Config.FloodMaxLitersPerSecond-.72f)<.000001f&&s.Config.LooseSnowPickupDepthMm==50,"authored saved tuning reaches runtime");
            float water=s.FloodWaterLiters,snow=s.SnowTankM3;
            t.enabled=false;Check(s.Config.FloodMaxLitersPerSecond==1.8f&&s.Config.LooseSnowPickupDepthMm==6,"disabling tuning restores original scene material settings");
            Check(s.FloodWaterLiters==water&&s.SnowTankM3==snow,"disabling tuning preserves existing tank stocks");
            t.enabled=true;t.ApplyRuntime(s.Config,game.calibration);
            t.simulation.FloodFlowMultiplier=.5f;t.ApplyRuntime(s.Config,game.calibration);t.ApplyRuntime(s.Config,game.calibration);
            Check(s.Config.FloodMaxLitersPerSecond==.9f,"live multiplier edits never compound");
            t.simulation.FloodFlowMultiplier=.4f;t.ApplyRuntime(s.Config,game.calibration);
            Check(JsonUtility.ToJson(game.calibration)==saved,"runtime experiments never mutate authored calibration");
            Check(s.Config.FloodCapacityLiters==738&&s.Config.WashCapacityLiters==310&&s.Config.SnowCapacityM3==3.54f,"all physical tank capacities preserved");
            ResidueGraphics(game);
            t.SampleTelemetry(game.Sim);Check(t.measuredTankDeliveryM3ps>0,"Inspector telemetry measures delivered snow");
            DrivenCut(t.CreateRuntimeConfig(game.calibration),"authored");
        }
        static void ResidueGraphics(IceResurfacerGame game)
        {
            game.NewSession(true);var s=game.Sim;var a=new OperatorMaterialValidation.Apparatus(s);
            for(int i=0;i<100;i++)s.Ice.DepositSnow(0,-5+i*.1f,.0001f,.12f);
            game.Surface.Refresh(s);Check(game.Surface.DepositedSnowChunks>0,"finite streak creates sparse snow meshes");
            var camera=game.DriverCamera;camera.transform.SetPositionAndRotation(new Vector3(0,4,0),Quaternion.Euler(90,0,0));camera.orthographic=true;camera.orthographicSize=1;
            var before=Capture(camera,"streak-before.png");UnityEngine.Object.Destroy(before);
            s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;s.Controls.BladeDepthMm=.35f;
            a.Line(0,-6,6);a.Drain();game.Surface.Refresh(s);
            int index=s.Ice.IndexAt(0,0);Check(s.Ice.Cells[index].SnowMm==0,"collected streak centre has exactly zero simulated snow");
            Check(game.Surface.StateTexture.GetPixel(index%s.Ice.Columns,index/s.Ice.Columns).g==0,"state texture snow channel clears to zero");
            Check(game.Surface.DepositedSnowChunks==0,"no stale snow triangles remain after complete pickup");
            var after=Capture(camera,"streak-after.png");
            var reference=new Simulation(Copy(s.Config));var clean=new OperatorMaterialValidation.Apparatus(reference);
            reference.Controls.HorizontalAuger=reference.Controls.VerticalAuger=true;reference.Controls.BladeDepthMm=.35f;clean.Line(0,-6,6);clean.Drain();
            game.Surface.Invalidate();game.Surface.Refresh(reference);var expected=Capture(camera,"streak-clean-reference.png");
            var actualPixels=after.GetPixels32();var cleanPixels=expected.GetPixels32();int maximum=0;
            // Central metre, away from neighbouring uncut dust and chunk boundaries.
            for(int y=128;y<384;y++)for(int x=192;x<320;x++)
            {int i=y*512+x;maximum=Math.Max(maximum,Math.Abs(actualPixels[i].r-cleanPixels[i].r));maximum=Math.Max(maximum,Math.Abs(actualPixels[i].g-cleanPixels[i].g));maximum=Math.Max(maximum,Math.Abs(actualPixels[i].b-cleanPixels[i].b));}
            Check(maximum<=2,"rendered recollected streak matches clean cut reference (max RGB error "+maximum+"/255)");
            UnityEngine.Object.Destroy(after);UnityEngine.Object.Destroy(expected);
            game.Surface.Invalidate();game.Surface.Refresh(s);
            Check(!ShaderUtil.ShaderHasError(Shader.Find("IceResurfacer/SpatialIce")),"spatial ice shader has no compilation errors");
        }
        static Texture2D Capture(Camera camera,string name)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(512,512,24);var texture=new Texture2D(512,512,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();File.WriteAllBytes(Out+name,texture.EncodeToPNG());return texture;}
            finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void Audit(IceResurfacerGame game)
        {
            var rig=game.GetComponentInChildren<IceMachineRig>();var w=rig.Collect();var m=w.Machine;var c=game.calibration;
            var eye=m.InverseTransformPoint(w.CameraMount.position);var eyeSeat=w.Seat.InverseTransformPoint(w.CameraMount.position);
            Log("CAMERA anchor '"+w.CameraMount.name+"' parent '"+w.CameraMount.parent.name+"' machine="+eye.ToString("F5")+" seat-local="+eyeSeat.ToString("F5")+" height adjustment="+game.operatorEyeHeight);
            Log("CAMERA seat machine="+m.InverseTransformPoint(w.Seat.position).ToString("F5")+" eye local rotation="+w.CameraMount.localEulerAngles.ToString("F3"));
            var anchors=rig.GetComponentsInChildren<Transform>(true).Where(x=>x.name.IndexOf("Driver Eye",StringComparison.OrdinalIgnoreCase)>=0||x.name.IndexOf("Controls View",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            Log("CAMERA named Driver Eye / Controls View anchors="+anchors.Length+" (Controls view is calculated in IceResurfacerGame.SelectCameraView)");
            foreach(var r in w.Seat.GetComponentsInChildren<Renderer>())
            {if(r.name.IndexOf("cushion",StringComparison.OrdinalIgnoreCase)>=0||r.name.IndexOf("back",StringComparison.OrdinalIgnoreCase)>=0){var b=IceConditionerAlignment.LocalBounds(new[]{r},w.Seat);Log("SEAT "+r.name+" seat-local bounds="+b.min.ToString("F5")+" .. "+b.max.ToString("F5"));}}
            float radius=IceMechanicalAlignment.GuideRollerRadius(w);var centre=m.InverseTransformPoint(w.GuideRoller.position);
            Log("GUIDE ROLLER independent assembly='"+w.GuideRoller.parent.name+"' centre="+centre.ToString("F5")+" radius="+radius.ToString("F5")+" outer reach="+(-centre.x+radius).ToString("F5"));
            foreach(var r in w.Conditioner.GetComponentsInChildren<Renderer>())
                if(r.name.Contains("side plate")){var b=IceConditionerAlignment.LocalBounds(new[]{r},m);Log("CONDITIONER "+r.name+" machine bounds="+b.min.ToString("F5")+" .. "+b.max.ToString("F5"));}
            Log("COLLISION conditioner half-width="+c.ConditionerCollisionHalfWidth+" front="+c.ConditionerCollisionFront+" rear="+c.ConditionerCollisionRear+" outline vertices="+(c.CollisionOutline==null?0:c.CollisionOutline.Length/2));
            var brush=w.BrushArm.GetComponent<IceBoardBrushRig>();var work=IceBoardBrushRig.ContactLocal(brush.mechanism,1);
            Log("BOARD BRUSH independent assembly='"+w.BrushArm.name+"' pivot=("+c.BrushPivotX+", "+c.BrushPivotZ+") arm="+c.BrushArmLength+" working centre="+work.ToString("F5")+" bristles radius="+IceBoardBrushRigEditor.VisualRadius(brush).ToString("F5")+" sim radius="+c.BrushRadius+" outer reach="+(-work.x+c.BrushRadius).ToString("F5"));
            Log("BOARD BRUSH draft matches simulation="+brush.mechanism.Matches(IceBoardBrushRig.Mechanism.From(c)));
        }
        // A representative driven full cut of the existing generated reference,
        // distinct from the operator's recorded route and the apparatus sweeps.
        static void DrivenCut(SimulatorConfig config,string label)
        {OperatorMaterialValidation.DrivenCut(config,Log,label);}
        static float Distance(RoutePoint p,Simulation s){float x=p.X-s.Vehicle.X,z=p.Z-s.Vehicle.Z;return Mathf.Sqrt(x*x+z*z);}
    }
}
