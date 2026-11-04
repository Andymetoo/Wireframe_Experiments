using System;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    // Run only in the disposable project. Never saves scenes, prefabs or calibration.
    [InitializeOnLoad]
    public static class IceSnowRealismValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/",Stage="Aurora.SnowRealism.Stage";
        const string Out=Root+"Validation~/AugersOff-20261104/";
        static int frames;
        static IceSnowRealismValidation(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Log(string text){Directory.CreateDirectory(Out);File.AppendAllText(Out+"unity-results.txt",text+"\n");Debug.Log(text);}
        static void Check(bool value,string message){OperatorMaterialValidation.Require(value,message);Log("PASS "+message);}
        public static void Run()
        {
            try
            {
                Check(Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"),"isolated project guard");
                File.WriteAllText(Out+"unity-results.txt","");
                EditorSceneManager.OpenScene(Root+"Scenes/IceResurfacer_AuthoredMachine.unity");
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
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                Check(game!=null&&game.Sim!=null,"initialized scene "+stage);game.enabled=false;
                Verify(game,stage==1?"authored":"procedural");
                if(stage==1){SessionState.SetInt(Stage,2);EditorApplication.ExitPlaymode();}
                else{SessionState.SetInt(Stage,0);Log("ALL SNOW REALISM UNITY CHECKS PASSED");EditorApplication.Exit(0);}
            }
            catch(Exception e){Fail(e);}
        }
        static void Verify(IceResurfacerGame game,string label)
        {
            game.NewSession(true);var s=game.Sim;var a=new OperatorMaterialValidation.Apparatus(s);
            double initial=OperatorMaterialValidation.SnowStock(s);s.Controls.BladeDepthMm=.35f;
            a.Line(0,-24,24);Refresh(game);
            Check(s.SpilledSnowM3>.018f,"substantial finite overflow "+label);
            Check(game.Surface.DepositedSnowChunks>0,"sparse faceted deposits visible "+label);
            var camera=game.DriverCamera;
            camera.transform.SetPositionAndRotation(new Vector3(0,5,10),Quaternion.Euler(90,0,0));camera.orthographic=true;camera.orthographicSize=3;
            Capture(camera,label+"-overflow.png");
            s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;a.Drain();
            a.Line(-1.265f,-25,25);a.Line(1.265f,-25,25);a.Drain();Refresh(game);
            int inspected=0;
            for(int i=0;i<s.Ice.Cells.Length;i++)
            {
                float x=s.Ice.CellX(i%s.Ice.Columns),z=s.Ice.CellZ(i/s.Ice.Columns);
                if(Math.Abs(Math.Abs(x)-1.265f)>.35f||z< -24||z>24)continue;
                CheckCell(s,game.Surface,i);inspected++;
            }
            Check(game.Surface.DepositedSnowChunks==0,"all recollected deposit meshes disabled "+label);
            Check(inspected>1000,"mesh/shader clean band inspected "+label);Capture(camera,label+"-recollected.png");
            OperatorMaterialValidation.Near(OperatorMaterialValidation.SnowStock(s)-initial-s.TotalExpandedShavingsM3,0,.00005,"Unity material conservation");
            Check(!ShaderUtil.ShaderHasError(Shader.Find("IceResurfacer/SpatialIce")),"ice shader compiled "+label);
            OperatorSnowRealismValidation.Driven(Log,JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(s.Config)),label);
            // Repeated refreshes after warm-up must reuse mesh buffers.
            for(int i=0;i<12;i++)game.Surface.Refresh(s);
            var watch=System.Diagnostics.Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<200;i++)game.Surface.Refresh(s);
            bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;watch.Stop();
            Log($"Unity surface scene={label} refreshes=200 ms={watch.Elapsed.TotalMilliseconds:F3} managed_bytes={bytes} vertices={game.Surface.DepositedSnowVertices}");
        }
        static void CheckCell(Simulation s,IceSurfaceView view,int index)
        {
            OperatorMaterialValidation.Require(s.Ice.Cells[index].SnowMm==0,"actual snow remained in cleanup band");
            OperatorMaterialValidation.Require(view.StateTexture.GetPixel(index%s.Ice.Columns,index/s.Ice.Columns).g==0,"white shader channel remained in cleanup band");
        }
        static void Refresh(IceResurfacerGame game)
        {for(int i=0;i<64;i++)game.Surface.Refresh(game.Sim);}
        static void Capture(Camera camera,string name)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(960,720,24);var texture=new Texture2D(960,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,720),0,0);texture.Apply();File.WriteAllBytes(Out+name,texture.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
        }
        static void Fail(Exception e){SessionState.SetInt(Stage,0);Log("FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
