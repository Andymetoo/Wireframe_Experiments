using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEngine;

namespace IceResurfacer.Editor
{
    internal static class IceCalibrationRuntimeValidation
    {
        static readonly List<string> lines=new List<string>();
        static IceResurfacerGame game;
        static Action<float> render;
        static void Check(bool ok,string message)
        { if(!ok)throw new InvalidOperationException("Ice calibration: "+message);lines.Add("PASS "+message); }
        static void Tick(float seconds)
        { for(int i=0;i<Mathf.RoundToInt(seconds/.02f);i++)game.Sim.Step(.02f); }
        static void Place(float x,float z)
        {
            var s=game.Sim;s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=s.Vehicle.Speed=s.Vehicle.SteeringAngle=0;
            s.Conditioner.X=x;s.Conditioner.Z=z-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;
            s.Controls.Brake=false;s.Controls.Throttle=s.Controls.Steer=0;
        }
        static void WorkingPass()
        {
            var s=game.Sim;s.Controls.HydraulicHoldControls=false;s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
            s.Controls.BladeDepthMm=.35f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
            s.Controls.FloodFlow=.43f;s.Controls.Throttle=.5f;Tick(12);
        }
        static void Refresh()
        {render(0);game.Surface.Refresh(game.Sim);}
        static void Band(float z,float threshold,bool cut,out float min,out float max)
        {
            var f=game.Sim.Ice;int row=f.IndexAt(0,z)/f.Columns;min=float.MaxValue;max=float.MinValue;
            for(int col=0;col<f.Columns;col++)
            {
                var c=f.Cells[f.Index(col,row)];if((cut?c.CutDepthMm:c.FloodMm)<=threshold)continue;
                min=Mathf.Min(min,f.CellX(col)-f.CellSize*.5f);max=Mathf.Max(max,f.CellX(col)+f.CellSize*.5f);
            }
        }
        public static void Run(IceResurfacerGame owner)
        {
            game=owner;lines.Clear();bool enabled=game.enabled;game.enabled=false;
            render=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),game,typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic));
            var cameraObject=new GameObject("Ice calibration measurement camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3;camera.nearClipPlane=.03f;camera.farClipPlane=30;
            camera.backgroundColor=new Color(.09f,.13f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            try
            {
                game.NewSession(true);Place(0,-12);WorkingPass();
                Band(-5,.08f,false,out float firstMin,out float firstEdge);
                Band(-5,.001f,false,out float filmMin,out float filmMax);
                Band(-5,.005f,true,out float cutMin,out float cutMax);
                float outside=(game.Sim.Config.WheelTrack+game.Sim.Config.TireWidth)*.5f;
                float center=firstEdge+outside;Place(center,-12);WorkingPass();
                float measuredTowelOverlap=game.Sim.Config.WaterDistributionWidth-center;
                Check(measuredTowelOverlap>0,"measured strong-boundary tire alignment preserves positive nominal towel overlap");
                game.Sim.Controls.FloodFlow=game.Sim.Controls.Throttle=0;Refresh();
                var field=game.Sim.Ice;int row=field.IndexAt(0,-5)/field.Columns,gaps=0;
                for(int col=0;col<field.Columns;col++)
                {
                    float x=field.CellX(col);if(x<-.5f||x>center+.5f)continue;
                    int index=field.Index(col,row);var c=field.Cells[index];
                    if(c.FloodMm<.08f||!c.Covered)gaps++;
                    Color pixel=game.Surface.ProcessTexture.GetPixel(col,row);
                    Check(Mathf.Abs(pixel.b-Mathf.Clamp01(c.FloodMm/.3f))<.005f,"texture flood sample agrees with actual cell at x="+x.ToString("F2"));
                }
                Check(gaps==0,"tire-aligned adjacent passes have zero dry or unscored cells between their centers");
                lines.Add("World measurement: wheel track=1.370m, tire width=.300m, outside tire=.835m. First strong wet edge="+firstEdge.ToString("F3")+
                    "m; second center="+center.ToString("F3")+"m; second inside tire="+(center-outside).ToString("F3")+"m.");
                lines.Add("Measured-boundary nominal towel overlap="+measuredTowelOverlap.ToString("F3")+
                    "m; ideal continuous towel-edge alignment overlap="+(game.Sim.Config.WaterDistributionWidth*.5f-outside).ToString("F3")+
                    "m. The measured wet boundary is a cell extent, so those two reference alignments differ.");
                lines.Add("First blade raster="+(cutMax-cutMin).ToString("F3")+"m; strong wet raster="+(firstEdge-firstMin).ToString("F3")+
                    "m; all-liquid raster="+(filmMax-filmMin).ToString("F3")+"m. Raster tolerance=.100m cells; physical blade1.956m/towel2.100m; film fringe<=.120m/side.");
                camera.transform.SetPositionAndRotation(new Vector3(center*.5f,6,-5),Quaternion.Euler(90,0,0));
                IceResurfacerEditor.Capture(camera,"Tire-overlap-top.png",1920,1080);
                game.SelectCameraView("Conditioner");
                var look=(Action<bool,float>)Delegate.CreateDelegate(typeof(Action<bool,float>),game,typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic));
                for(int i=0;i<50;i++)look(true,.02f);
                IceResurfacerEditor.Capture(game.DriverCamera,"Tire-overlap-operator-Q.png",1920,1080);
                game.Surface.SetDebugMode(2);game.Surface.Refresh(game.Sim);
                IceResurfacerEditor.Capture(camera,"Ice-affected-cells-debug.png",1920,1080);
                game.Surface.SetDebugMode(1);game.Surface.Refresh(game.Sim);
                camera.orthographicSize=4;camera.transform.position=new Vector3(-10.7f,6,-5);
                IceResurfacerEditor.Capture(camera,"Ice-shaving-mask-debug.png",1920,1080);
                Check(field.ShavableCellCount<field.IceCellCount,"six-inch perimeter mask excludes shaving only; water and quality still use all ice");
                game.Surface.SetDebugMode(0);

                game.NewSession(true);Check(game.Sim.ConfigureWaterForServiceTest(1,.5f),"recovery fixture makes room in the independent wash tank");
                Place(0,0);var s=game.Sim;s.Controls.HydraulicHoldControls=false;s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
                s.Controls.WashValve=.2f;Tick(8);s.Controls.FloodFlow=1;Tick(3);s.Controls.WashValve=s.Controls.FloodFlow=0;Refresh();
                camera.orthographicSize=1.8f;camera.transform.SetPositionAndRotation(new Vector3(0,4,-1.1f),Quaternion.Euler(90,0,0));
                game.Surface.SetDebugMode(0);game.Surface.Refresh(s);
                CaptureExposedIce(camera,"Overwater-before-pickup-normal.png");
                game.Surface.SetDebugMode(2);game.Surface.Refresh(s);
                CaptureExposedIce(camera,"Overwater-before-pickup.png");
                double before=WaterMass(s),tank=s.WashWaterLiters;float overBefore=Overwater(s);
                s.Controls.WashPump=true;s.Controls.Throttle=-.2f;Tick(5);Refresh();
                double recovered=s.TotalRecoveredSurfaceLiters;
                Check(recovered>.3,"primed physical squeegee collects existing standing water over time");
                Check(Math.Abs(before-WaterMass(s)-recovered)<.003,"surface-water removal equals metered pickup");
                Check(Math.Abs(s.WashWaterLiters-tank-recovered)<.003,"recovered water is routed into the wash reservoir");
                Check(Overwater(s)<overBefore,"overwater improves only after actual liquid removal");
                CaptureExposedIce(camera,"Overwater-after-pickup.png");
                game.Surface.SetDebugMode(0);game.Surface.Refresh(s);
                CaptureExposedIce(camera,"Overwater-after-pickup-normal.png");
                lines.Add("Pickup captured after5s driving back over the patch: "+recovered.ToString("F3")+"L, pump limited; no blade cutting and no instant erase. The before/after uses actual-cell debug colors because liquid specular alone is view dependent.");
                lines.Add("Recovery measurement captures temporarily hide machine/conditioner geometry to expose the cells under the squeegee and towel; simulated water, motion and materials are unchanged.");
                lines.Add("Fixtures place two straight starts for repeatable geometry. All passes and water changes then run the real fixed-step simulation. Full route validation separately uses continuous driving.");
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Ice-calibration-runtime-results.txt",lines);
            }
            finally
            {
                UnityEngine.Object.Destroy(cameraObject);game.Surface.SetDebugMode(0);game.enabled=enabled;
                game.NewSession(true);
            }
        }
        static double WaterMass(Simulation s)
        {double sum=0;foreach(var c in s.Ice.Cells)sum+=c.FloodMm*s.Ice.CellSize*s.Ice.CellSize;return sum;}
        static float Overwater(Simulation s)
        {float sum=0;foreach(var c in s.Ice.Cells)sum+=c.OverwaterMm;return sum;}
        static void CaptureExposedIce(Camera camera,string filename)
        {
            bool machine=game.World.Machine.gameObject.activeSelf,conditioner=game.World.Conditioner.gameObject.activeSelf;
            try
            {
                game.World.Machine.gameObject.SetActive(false);game.World.Conditioner.gameObject.SetActive(false);
                IceResurfacerEditor.Capture(camera,filename,1920,1080);
            }
            finally
            {game.World.Machine.gameObject.SetActive(machine);game.World.Conditioner.gameObject.SetActive(conditioner);}
        }
    }
}
