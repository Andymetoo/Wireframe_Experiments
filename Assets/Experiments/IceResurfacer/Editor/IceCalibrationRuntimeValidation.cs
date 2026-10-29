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
        static void WorkingPass(float blade=.35f,float flood=.43f)
        {
            var s=game.Sim;s.Controls.HydraulicHoldControls=false;s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
            s.Controls.BladeDepthMm=blade;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
            s.Controls.FloodFlow=flood;s.Controls.Throttle=.5f;Tick(12);
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
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3;camera.nearClipPlane=.03f;camera.farClipPlane=180;
            camera.aspect=1920f/1080f;
            camera.backgroundColor=new Color(.09f,.13f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            try
            {
                game.NewSession(true);Place(0,-12);WorkingPass();
                Band(-5,.08f,false,out float firstMin,out float firstEdge);
                Band(-5,.001f,false,out float filmMin,out float filmMax);
                Band(-5,.005f,true,out float cutMin,out float cutMax);
                MeasureRenderedWaterWidth(camera);
                CaptureOperatorEdge("Ice-single-pass-operator-Q.png");
                float outside=(game.Sim.Config.WheelTrack+game.Sim.Config.TireWidth)*.5f;
                float center=firstEdge+outside;Place(center,-12);WorkingPass();
                float measuredTowelOverlap=game.Sim.Config.WaterDistributionWidth-center;
                Check(measuredTowelOverlap>0,"measured strong-boundary tire alignment preserves positive nominal towel overlap");
                game.Sim.Controls.FloodFlow=game.Sim.Controls.Throttle=0;Refresh();
                var field=game.Sim.Ice;int row=field.IndexAt(0,-5)/field.Columns,gaps=0;
                Vector4 gridWorld=game.World.IceMesh.sharedMaterial.GetVector("_GridWorld");
                Check(Mathf.Abs((field.CellX(7)-gridWorld.x)*gridWorld.z-7.5f/field.Columns)<.000001f&&
                    Mathf.Abs((field.CellZ(19)-gridWorld.y)*gridWorld.w-19.5f/field.Rows)<.000001f,
                    "render coordinates map physical cell centers to exact state-texture centers");
                Check(game.Surface.StateTexture.width==field.Columns&&game.Surface.StateTexture.height==field.Rows,
                    "state texture retains the full "+field.Columns+"x"+field.Rows+" simulation grid");
                for(int col=0;col<field.Columns;col++)
                {
                    float x=field.CellX(col);if(x<-.5f||x>center+.5f)continue;
                    int index=field.Index(col,row);var c=field.Cells[index];
                    if(c.FloodMm<.08f||!c.Covered)gaps++;
                    Color pixel=game.Surface.ProcessTexture.GetPixel(col,row);
                    Check(Mathf.Abs(pixel.b-Mathf.Clamp01(c.FloodMm/.3f))<.005f,"texture flood sample agrees with actual cell at x="+x.ToString("F2"));
                }
                Check(gaps==0,"tire-aligned adjacent passes have zero dry or unscored cells between their centers");
                lines.Add("World measurement: wheel track="+game.Sim.Config.WheelTrack.ToString("F3")+"m, nominal tire width="+game.Sim.Config.TireWidth.ToString("F3")+"m, outside tire="+outside.ToString("F4")+"m. First strong wet edge="+firstEdge.ToString("F3")+
                    "m; second center="+center.ToString("F3")+"m; second inside tire="+(center-outside).ToString("F3")+"m.");
                lines.Add("Measured-boundary nominal towel overlap="+measuredTowelOverlap.ToString("F3")+
                    "m; ideal continuous towel-edge alignment overlap="+(game.Sim.Config.WaterDistributionWidth*.5f-outside).ToString("F3")+
                    "m. The measured wet boundary is a cell extent, so those two reference alignments differ.");
                lines.Add("First blade raster="+(cutMax-cutMin).ToString("F3")+"m; strong wet raster="+(firstEdge-firstMin).ToString("F3")+
                    "m; all-liquid raster="+(filmMax-filmMin).ToString("F3")+"m. Cell size="+field.CellSize.ToString("F3")+"m; physical blade1.956m/towel2.100m; film fringe<=.120m/side.");
                camera.transform.SetPositionAndRotation(new Vector3(center*.5f,6,-5),Quaternion.Euler(90,0,0));
                IceResurfacerEditor.Capture(camera,"Tire-overlap-top.png",1920,1080);
                CaptureOperatorEdge("Tire-overlap-operator-Q.png");
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
                CaptureWearAndAging(camera);
                CaptureStationaryPooling(camera);
                ValidateMapSurfaceAgreement(camera);
                CaptureReflectionPolish(camera);
                ValidatePresentationInvalidation();
                ValidateAgingBudget();
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

        static void ValidateMapSurfaceAgreement(Camera camera)
        {
            game.NewSession(true);Place(0,-12);WorkingPass();Place(2.05f,-12);WorkingPass();
            game.Sim.Controls.Throttle=game.Sim.Controls.FloodFlow=0;Refresh();
            var s=game.Sim;var field=s.Ice;int gaps=0,wetUncut=0,snowy=0;
            Color32[] map=game.Surface.MapTexture.GetPixels32();
            for(int row=0;row<field.Rows;row++)
            {
                float z=field.CellZ(row);if(z< -9||z> -5)continue;
                for(int col=0;col<field.Columns;col++)
                {
                    float x=field.CellX(col);if(x<.3f||x>1.75f)continue;
                    int i=field.Index(col,row);var cell=field.Cells[i];
                    Color32 expected=IceSurfaceView.TreatmentColor(IceCoverage.Classify(cell,s.Config));
                    if(map[i].r!=expected.r||map[i].g!=expected.g||map[i].b!=expected.b)
                        throw new InvalidOperationException("Minimap classification texture disagrees with actual ice cell "+i);
                    if(cell.Covered)continue;
                    gaps++;
                    if(cell.CutDepthMm<=IceCoverage.RequiredCutMm(s.Config)&&cell.FloodMm>IceCoverage.RequiredFloodMm)wetUncut++;
                    if(cell.SnowMm>=IceCoverage.MaximumLooseSnowMm)snowy++;
                }
            }
            Check(gaps>0&&wetUncut==gaps,"narrow visually wet seam contains real uncut cells, without lowered coverage rules");
            Check(map.Length==field.Cells.Length,"categorical map retains every actual 5cm state sample");
            camera.orthographic=true;camera.orthographicSize=2.1f;
            camera.transform.SetPositionAndRotation(new Vector3(1.025f,6,-7),Quaternion.Euler(90,0,0));
            CaptureExposedIce(camera,"Minimap-wet-uncut-seam-normal.png");
            ValidateRenderedSeam(camera,field,s);
            game.Surface.SetDebugMode(2);game.Surface.Refresh(s);
            CaptureExposedIce(camera,"Minimap-wet-uncut-seam-actual.png");
            game.Surface.SetDebugMode(0);game.Surface.Refresh(s);
            File.WriteAllBytes("Assets/Experiments/IceResurfacer/Validation~/Minimap-treatment-map.png",game.Surface.MapTexture.EncodeToPNG());
            File.WriteAllBytes("Assets/Experiments/IceResurfacer/Validation~/Minimap-area-overview.png",game.Surface.OverviewTexture.EncodeToPNG());
            Check(game.Surface.OverviewTexture.width==130&&game.Surface.OverviewTexture.height==300,
                "operator overview aggregates sixteen physical5cm cells into independent20cm pixels");
            lines.Add("Minimap seam fixture: "+gaps+" missing interior cells, all actually wet but uncut; "+snowy+" also carry excess loose snow. Default overview is area-weighted20cm; quarter-pixel contributions retain a5cm seam rather than randomly omitting it. Exact cell inspector and F2 retain original thresholds. No states or scoring thresholds are filled in.");
            game.NewSession(true);Refresh();
            File.WriteAllBytes("Assets/Experiments/IceResurfacer/Validation~/Minimap-initial-overview.png",game.Surface.OverviewTexture.EncodeToPNG());
        }
        static void ValidateRenderedSeam(Camera camera,IceField field,Simulation s)
        {
            var material=game.World.IceMesh.sharedMaterial;var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                material.SetFloat("_DebugMode",3);CaptureExposedIce(camera,"Minimap-seam-rendered-liquid.png");
                image.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/Minimap-seam-rendered-liquid.png"));
                var pixels=image.GetPixels32();int row=field.IndexAt(0,-7)/field.Columns,wetSamples=0,uncutSamples=0;
                int span=IceOverview.CellsPerPixel(field),overviewRow=row/span;
                for(int col=0;col<field.Columns;col++)
                {
                    float x=field.CellX(col);if(x<.3f||x>1.75f)continue;
                    var cell=field.Cells[field.Index(col,row)];if(cell.FloodMm<=IceCoverage.RequiredFloodMm)continue;
                    Vector3 viewport=camera.WorldToViewportPoint(new Vector3(x,0,field.CellZ(row)));
                    int px=Mathf.Clamp((int)(viewport.x*image.width),0,image.width-1),py=Mathf.Clamp((int)(viewport.y*image.height),0,image.height-1);
                    Check(pixels[py*image.width+px].r>85,"actual wetted seam cell has nonzero GPU wet response at x="+x.ToString("F3"));
                    wetSamples++;
                    if(cell.CutDepthMm<=IceCoverage.RequiredCutMm(s.Config))uncutSamples++;
                    var summary=IceOverview.Summarize(field,s.Config,col/span*span,overviewRow*span,span);
                    Color32 expected=IceSurfaceView.OverviewColor(summary);
                    Color32 actual=game.Surface.OverviewTexture.GetPixel(col/span,overviewRow);
                    Check(actual.r==expected.r&&actual.g==expected.g&&actual.b==expected.b,
                        "overview pixel equals measured cut/water area fractions at x="+x.ToString("F3"));
                }
                Check(wetSamples>20&&uncutSamples>0,"same physical seam is continuous in GPU wetness but retains real cutting misses");
                int wetCells=0;foreach(var cell in field.Cells)if(cell.IsIce&&cell.FloodMm>IceCoverage.RequiredFloodMm)wetCells++;
                Check(Mathf.Abs(s.GetReport().WaterCoverage-(float)wetCells/field.IceCellCount)<.000001f,
                    "scored water coverage uses exactly the wetted cells represented by the map");
                lines.Add("Rendered seam correspondence: "+wetSamples+" physically wetted samples, "+uncutSamples+" genuinely uncut; all have measured GPU wet response.20cm pixels equal exact area-weighted treatment fractions and water score equals raw wetted count.");
            }
            finally{material.SetFloat("_DebugMode",0);UnityEngine.Object.Destroy(image);}
        }

        static void CaptureReflectionPolish(Camera camera)
        {
            game.NewSession(true);Place(0,-12);WorkingPass(.35f,.43f);
            game.Sim.Controls.Throttle=game.Sim.Controls.FloodFlow=0;Refresh();
            var material=game.World.IceMesh.sharedMaterial;double water=WaterMass(game.Sim);
            camera.orthographic=false;camera.fieldOfView=62;
            camera.transform.SetPositionAndRotation(new Vector3(-2.4f,1.85f,-10),Quaternion.LookRotation(new Vector3(2.4f,-1.85f,9)));
            material.SetFloat("_FixtureReflectionPolish",0);CaptureExposedIce(camera,"Ice-reflection-before.png");
            material.SetFloat("_FixtureReflectionPolish",1);CaptureExposedIce(camera,"Ice-reflection-after.png");
            Check(Math.Abs(water-WaterMass(game.Sim))<.0000001,"identical-state reflection comparison changes no flood volume or physical wear");
            var before=new Texture2D(2,2);var after=new Texture2D(2,2);
            try
            {
                before.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/Ice-reflection-before.png"));
                after.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/Ice-reflection-after.png"));
                var a=before.GetPixels32();var b=after.GetPixels32();long difference=0;int changed=0;
                for(int i=0;i<a.Length;i++)
                {
                    int delta=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
                    difference+=delta;if(delta>3)changed++;
                }
                double mean=difference/(a.Length*3.0*255);
                Check(changed>100&&mean<.04,"ceiling-fixture polish remains localized at identical camera/water state (mean RGB delta "+mean.ToString("F5")+")");
                lines.Add("Reflection comparison uses the same camera, water and groove state. Added lights/probes/render targets: zero. Existing ceiling diffuser positions replace periodic invented glints; "+changed+" pixels differ by more than three combined RGB levels.");
            }
            finally {UnityEngine.Object.Destroy(before);UnityEngine.Object.Destroy(after);camera.orthographic=true;}
        }
        static void CaptureOperatorEdge(string name)
        {
            game.SelectCameraView("Conditioner");
            var look=(Action<bool,float>)Delegate.CreateDelegate(typeof(Action<bool,float>),game,
                typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic));
            for(int i=0;i<50;i++)look(true,.02f);
            IceResurfacerEditor.Capture(game.DriverCamera,name,1920,1080);
        }
        static void MeasureRenderedWaterWidth(Camera camera)
        {
            Refresh();camera.orthographicSize=3;
            camera.transform.SetPositionAndRotation(new Vector3(0,6,-5),Quaternion.Euler(90,0,0));
            var material=game.World.IceMesh.sharedMaterial;var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            const string name="Ice-water-response-top.png";
            try
            {
                material.SetFloat("_DebugMode",3);CaptureExposedIce(camera,name);
                Check(image.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/"+name)),"rendered water-response capture loads");
                Color32[] samples=image.GetPixels32();int row=image.height/2,first=image.width,last=-1;
                for(int x=0;x<image.width;x++)
                    if(samples[row*image.width+x].r>=153){first=Mathf.Min(first,x);last=x;}
                float metersPerPixel=camera.orthographicSize*2/image.height;
                float width=Mathf.Max(0,last-first+1)*metersPerPixel;
                Check(width>=game.Sim.Config.WaterDistributionWidth-.075f,
                    "rendered visible wet band spans the physical towel ("+width.ToString("F3")+"m)");
                Check(width<=game.Sim.Config.WaterDistributionWidth+2*game.Sim.Config.WaterFilmSpreadMeters+2*game.Sim.Ice.CellSize,
                    "rendered response stays within finite supplied film plus interpolation support");
                lines.Add("Rendered wet width is measured from a 1920x1080 GPU response capture at 60% display intensity; it is not inferred from core cell extents. One image pixel="+metersPerPixel.ToString("F5")+"m.");
            }
            finally{material.SetFloat("_DebugMode",0);UnityEngine.Object.Destroy(image);}
        }
        static void CaptureWearAndAging(Camera camera)
        {
            game.NewSession(true);Place(0,-12);Refresh();
            camera.orthographic=true;camera.orthographicSize=3;
            camera.transform.SetPositionAndRotation(new Vector3(0,7,-5),Quaternion.Euler(90,0,0));
            CaptureExposedIce(camera,"Ice-wear-before-top.png");
            camera.orthographic=false;camera.fieldOfView=58;
            camera.transform.position=new Vector3(-5.6f,3.2f,-10.5f);
            camera.transform.LookAt(new Vector3(0,.005f,-5));
            CaptureExposedIce(camera,"Ice-wear-before-oblique.png");
            float[] centers={-3.6f,-1.2f,1.2f,3.6f};
            float[] depths={.08f,.22f,.60f,.60f};
            for(int lane=0;lane<centers.Length;lane++)
            { Place(centers[lane],-12);WorkingPass(depths[lane],lane==3?.43f:0); }
            var s=game.Sim;var field=s.Ice;
            s.Controls.Throttle=s.Controls.FloodFlow=s.Controls.WashValve=0;s.Controls.Brake=true;
            s.Controls.ConditionerTarget=1;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=false;
            Refresh();
            float light=AverageScratch(-3.6f),deep=AverageScratch(-1.2f),clean=AverageScratch(1.2f);
            Check(light>deep+.08f&&deep>clean+.03f,"real light, deeper and clean dry cuts retain distinguishable groove severity");
            lines.Add("Wear comparison lanes left to right: blade .08mm dry / .22mm dry / .60mm dry / .60mm with water. Actual mean residual Scratch="+
                light.ToString("F3")+" / "+deep.ToString("F3")+" / "+clean.ToString("F3")+". No cosmetic scratch erasure.");
            CaptureExposedIce(camera,"Ice-wear-after-oblique.png");
            camera.orthographic=true;camera.orthographicSize=3;
            camera.transform.SetPositionAndRotation(new Vector3(0,7,-5),Quaternion.Euler(90,0,0));
            CaptureExposedIce(camera,"Ice-wear-after-top.png");
            camera.orthographic=false;camera.fieldOfView=50;
            camera.transform.position=new Vector3(7f,5f,-11f);
            camera.transform.LookAt(new Vector3(2.7f,.005f,-5.8f));
            int sample=field.IndexAt(3.6f,-5);
            float start=s.Time,previous=float.MaxValue;double mass=WaterMass(s);
            int[] ages={0,300,600,1200,1800};
            foreach(int age in ages)
            {
                if(age>0)Tick(Mathf.Max(0,age-(s.Time-start)));
                Refresh();
                float wet=field.Wetness(sample,s.Time);
                Check(wet<previous,"actual liquid wetness decreases at age "+age+"s ("+wet.ToString("F4")+")");
                previous=wet;
                CaptureExposedIce(camera,"Ice-wet-age-"+(age/60).ToString("D2")+"min.png");
            }
            Check(Math.Abs(WaterMass(s)-mass)<.003,"visible wet aging changes phase without deleting retained water");
            float freshLight=PatchBrightness(camera,"Ice-wet-age-00min.png",3.0f,4.2f,-7f,-3.4f);
            float settledLight=PatchBrightness(camera,"Ice-wet-age-30min.png",3.0f,4.2f,-7f,-3.4f);
            Check(settledLight-freshLight>.035f,"actual rendered wet strip visibly loses dark liquid absorption after30min (RGB rise "+
                ((settledLight-freshLight)*255).ToString("F2")+"/255)");
            lines.Add("Same physical wet-strip pixel samples: fresh meanRGB="+(freshLight*255).ToString("F2")+
                ",30min="+(settledLight*255).ToString("F2")+"; camera frames the strip with180m far plane, correcting the old30m arena clipping artifact.");
            lines.Add("Aging captures use real fixed-step elapsed time at fresh/5/10/20/30 minutes; freeze constant="+s.Config.FreezeSeconds.ToString("F0")+
                "s. Machine, conditioner and the separate articulating drawbar are hidden only in measurement captures to expose the surface. Cubic reconstruction has finite support of two 5cm samples; no full-sheet blur or cosmetic water ribbon.");
        }
        static float AverageScratch(float center)
        {
            var f=game.Sim.Ice;float total=0;int count=0;
            for(int row=0;row<f.Rows;row++)
            {
                float z=f.CellZ(row);if(z<-8||z>-3)continue;
                for(int col=0;col<f.Columns;col++)
                {if(Mathf.Abs(f.CellX(col)-center)>.7f)continue;total+=f.Cells[f.Index(col,row)].Scratch;count++;}
            }
            return total/Mathf.Max(1,count);
        }
        static void CaptureStationaryPooling(Camera camera)
        {
            game.NewSession(true);Place(0,0);var s=game.Sim;
            s.Controls.HydraulicHoldControls=false;s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
            s.Controls.Brake=true;s.Controls.BladeDepthMm=0;s.Controls.WashValve=0;
            camera.orthographic=false;camera.fieldOfView=50;
            camera.transform.position=new Vector3(-3.7f,4.6f,-5.2f);
            camera.transform.LookAt(new Vector3(0,0,-1.54f));Refresh();
            CaptureExposedIce(camera,"Ice-stationary-flood-00s.png");
            s.Controls.FloodFlow=1;
            float previous=0;double previousArea=0;
            foreach(int seconds in new[]{5,15,30})
            {
                Tick(seconds-previous);previous=seconds;Refresh();
                double mass=WaterMass(s),area=0,liquid=0;float peak=0;
                foreach(var cell in s.Ice.Cells)
                {
                    if(!cell.IsIce)continue;
                    float depth=s.Ice.LiquidDepth(cell,s.Time);liquid+=depth*s.Ice.CellSize*s.Ice.CellSize;
                    if(cell.FloodMm>.08f)area+=s.Ice.CellSize*s.Ice.CellSize;
                    peak=Mathf.Max(peak,depth);
                }
                Check(Math.Abs(mass-s.TotalFloodUsedLiters)<.04,"stationary "+seconds+"s water mass remains finite and fully accounted");
                Check(area>previousArea,"stationary wet footprint expands through actual neighbour redistribution at "+seconds+"s");
                previousArea=area;
                string name="Ice-stationary-flood-"+seconds.ToString("D2")+"s.png";
                CaptureExposedIce(camera,name);
                lines.Add("Stationary flood "+seconds+"s: released "+s.TotalFloodUsedLiters.ToString("F3")+"L, liquid "+liquid.ToString("F3")+
                    "L, frozen "+(mass-liquid).ToString("F3")+"L, wetarea "+area.ToString("F3")+"m2, peak "+peak.ToString("F3")+"mm; no ice runoff at this interior location.");
            }
            s.Controls.FloodFlow=0;
            float dryLight=PatchBrightness(camera,"Ice-stationary-flood-00s.png",-.8f,.8f,-1.75f,-1.3f);
            float pooledLight=PatchBrightness(camera,"Ice-stationary-flood-30s.png",-.8f,.8f,-1.75f,-1.3f);
            Check(dryLight-pooledLight>.06f,"actual54L pool visibly darkens the physical outlet region (RGB drop "+
                ((dryLight-pooledLight)*255).ToString("F2")+"/255)");
            var before=new Texture2D(2,2);var after=new Texture2D(2,2);
            try
            {
                before.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/Ice-stationary-flood-05s.png"));
                after.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/Ice-stationary-flood-30s.png"));
                var a=before.GetPixels32();var b=after.GetPixels32();int changed=0;
                for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>6)changed++;
                Check(changed>1000,"long stationary pool visibly differs from short stop ("+changed+" changed pixels)");
            }
            finally{UnityEngine.Object.Destroy(before);UnityEngine.Object.Destroy(after);camera.orthographic=true;}
        }
        static float PatchBrightness(Camera camera,string filename,float minX,float maxX,float minZ,float maxZ)
        {
            var texture=new Texture2D(2,2);
            try
            {
                texture.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/"+filename));
                var pixels=texture.GetPixels32();double sum=0;int count=0;
                for(int z=0;z<=24;z++)for(int x=0;x<=16;x++)
                {
                    Vector3 p=camera.WorldToViewportPoint(new Vector3(Mathf.Lerp(minX,maxX,x/16f),0,Mathf.Lerp(minZ,maxZ,z/24f)));
                    if(p.z<=0||p.x<0||p.x>=1||p.y<0||p.y>=1)continue;
                    var color=pixels[(int)(p.y*texture.height)*texture.width+(int)(p.x*texture.width)];
                    sum+=(color.r+color.g+color.b)/(3.0*255);count++;
                }
                Check(count>300,"physical comparison patch remains framed in "+filename);
                return (float)(sum/Math.Max(1,count));
            }
            finally{UnityEngine.Object.Destroy(texture);}
        }
        static void ValidatePresentationInvalidation()
        {
            game.NewSession(true);var s=game.Sim;var field=s.Ice;var surface=game.Surface;
            // Explicit presentation apparatus: the production deposit APIs own
            // both stocks. Adding water must leave the existing snow mesh alone.
            field.DepositSnow(0,0,.005f,.35f);surface.Refresh(s);
            Check(surface.LastSnowChunksRebuilt>0&&surface.DepositedSnowChunks>0,
                "actual snow deposition invalidates and builds its sparse height mesh");
            float height=surface.DepositedSnowMaximumHeight;
            field.DepositWater(0,0,.5f,s.Time,.35f);surface.Refresh(s);
            Check(surface.LastSnowChunksRebuilt==0&&surface.DepositedSnowMaximumHeight==height,
                "water-only physical changes preserve the existing snow mesh without rescans or rebuilds");
            Check(surface.LastAgingCellsReused>0&&surface.LastAgingCellsReused==surface.LastAgingCellsUpdated,
                "newly dirtied wet cells reuse their exact phase result during the same aging pass");
            int marked=0;
            for(int i=0;i<field.Cells.Length&&marked<4096;i++)
                if(field.Cells[i].IsIce){field.MarkCellChanged(i);marked++;}
            surface.Refresh(s);
            Check(surface.LastCellsUpdated==marked&&surface.LastTextureUploads==0&&
                surface.LastOverviewPixelsUpdated==0&&surface.LastSnowChunksRebuilt==0,
                "4096 unchanged dirty cells cause no texture uploads, overview resummaries or snow rebuilds");
            int changed=field.IndexAt(3,0);var cell=field.Cells[changed];
            field.AddWater(ref cell,.09f,s.Time);field.UpdateQuality(ref cell);
            field.Cells[changed]=cell;field.MarkCellChanged(changed);surface.Refresh(s);
            int span=IceOverview.CellsPerPixel(field),col=changed%field.Columns,row=changed/field.Columns;
            Color32 expected=IceSurfaceView.OverviewColor(IceOverview.Summarize(field,s.Config,col/span*span,row/span*span,span));
            Color32 actual=surface.OverviewTexture.GetPixel(col/span,row/span);
            Check(surface.LastOverviewPixelsUpdated==1&&surface.LastTextureUploads>0&&
                actual.r==expected.r&&actual.g==expected.g&&actual.b==expected.b,
                "a real water classification crossing immediately updates its exact area-weighted overview pixel");
            lines.Add("Presentation invalidation apparatus: water over existing deposited snow rebuilds zero chunks; "+
                marked+" unchanged dirty cells upload zero textures. Exact water classification crossings remain visible immediately; "+
                "the following full-sheet phase test independently checks bounded aging and removal.");
        }
        static void ValidateAgingBudget()
        {
            game.NewSession(true);var s=game.Sim;var field=s.Ice;var surface=game.Surface;
            int last=-1;
            // A deliberately whole-wet fixture exercises the late-run workload;
            // normal gameplay still creates water only through the real outlets.
            for(int i=0;i<field.Cells.Length;i++)
            {
                var cell=field.Cells[i];if(!cell.IsIce)continue;
                cell.LiquidMm=cell.FloodMm=s.Config.GoodFloodDepthMm*(.35f+(i%101)*.006f);
                cell.FrozenFloodMm=0;cell.LiquidUpdateTime=cell.LastWetTime=s.Time;
                field.Cells[i]=cell;last=i;
            }
            field.MarkChanged();surface.Invalidate();surface.Refresh(s);
            Check(surface.ActiveWetCells==field.IceCellCount&&surface.LastCellsUpdated==field.Cells.Length,
                "explicit whole-wet invalidation updates every cell exactly");
            // This cell lies beyond the first aging slice. Physical pickup/clear
            // changes must reach the texture immediately through dirty tracking.
            var cleared=field.Cells[last];cleared.FrozenFloodMm=cleared.FloodMm;cleared.LiquidMm=0;
            field.Cells[last]=cleared;field.MarkCellChanged(last);surface.Refresh(s);
            Check(surface.StateTexture.GetPixel(last%field.Columns,last/field.Columns).a==0,
                "dirty liquid removal is visible immediately outside the aging cursor");
            int maximumAged=0;
            for(int refresh=0;refresh<10;refresh++)
            {
                Tick(.16f);surface.Refresh(s);
                maximumAged=Mathf.Max(maximumAged,surface.LastAgingCellsUpdated);
                Check(surface.LastAgingCellsUpdated<=IceSurfaceView.AgingCellsPerRefresh,
                    "whole-wet aging refresh "+(refresh+1)+" respects the 65536-cell budget");
            }
            Color32[] actual=surface.StateTexture.GetPixels32();int maximumError=0;
            for(int i=0;i<actual.Length;i++)
            {
                int expected=Mathf.RoundToInt(field.Wetness(i,s.Time)*255);
                maximumError=Mathf.Max(maximumError,Mathf.Abs(actual[i].a-expected));
            }
            Check(maximumError<=2,"all whole-wet alphas stay within 2/255 of exact phase after ten 0.16s refreshes (max "+maximumError+"/255)");
            // Bypass dirty tracking only in this explicit stress fixture, so the
            // aging cursor itself must retire every entry through swap removal.
            for(int i=0;i<field.Cells.Length;i++)
            {
                var cell=field.Cells[i];cell.FrozenFloodMm=cell.FloodMm;cell.LiquidMm=0;field.Cells[i]=cell;
            }
            for(int refresh=0;refresh<10;refresh++)surface.Refresh(s);
            actual=surface.StateTexture.GetPixels32();int visible=0;
            foreach(var pixel in actual)if(pixel.a!=0)visible++;
            Check(surface.ActiveWetCells==0&&visible==0,"bounded swap removal clears every expired cell, including the final GPU upload");
            var rewetted=field.Cells[last];field.AddWater(ref rewetted,.15f,s.Time);field.Cells[last]=rewetted;
            field.MarkCellChanged(last);surface.Refresh(s);
            Check(surface.ActiveWetCells==1&&surface.StateTexture.GetPixel(last%field.Columns,last/field.Columns).a>.45f,
                "a retired cell can become visibly wet again through dirty tracking");
            Tick(.16f);surface.Invalidate();surface.Refresh(s);
            Check(surface.LastCellsUpdated==field.Cells.Length&&surface.LastAgingCellsUpdated==0&&
                Mathf.Abs(surface.StateTexture.GetPixel(last%field.Columns,last/field.Columns).a-field.Wetness(last,s.Time))<=1f/255,
                "explicit invalidation remains exact without a redundant second aging scan");
            lines.Add("Late-wet aging: "+field.IceCellCount+" active ice cells; maximum ordinary aging work="+maximumAged+
                " cells/refresh. Dirty material changes bypass the visual budget; physical freezing and pickup remain exact.");
        }
        static void CaptureExposedIce(Camera camera,string filename)
        {
            bool machine=game.World.Machine.gameObject.activeSelf,conditioner=game.World.Conditioner.gameObject.activeSelf;
            var drawbar=game.transform.Find("Articulating conditioner drawbar");
            bool drawbarActive=drawbar!=null&&drawbar.gameObject.activeSelf;
            try
            {
                game.World.Machine.gameObject.SetActive(false);game.World.Conditioner.gameObject.SetActive(false);
                if(drawbar!=null)drawbar.gameObject.SetActive(false);
                IceResurfacerEditor.Capture(camera,filename,1920,1080);
            }
            finally
            {
                game.World.Machine.gameObject.SetActive(machine);game.World.Conditioner.gameObject.SetActive(conditioner);
                if(drawbar!=null)drawbar.gameObject.SetActive(drawbarActive);
            }
        }
    }
}
