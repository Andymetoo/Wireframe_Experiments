using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    // Explicit isolated-Editor tests. Offline DSP capture never plays test audio
    // through the user's device and never changes the original project settings.
    [InitializeOnLoad]
    public static class IceSpatialAudioValidation
    {
        const string Key="IceResurfacer.SpatialAudioValidation";
        const string Root="Assets/Experiments/IceResurfacer/Validation~/SpatialAudio/";
        static readonly List<string> checks=new List<string>(),rows=new List<string>();
        static readonly List<float> pcm=new List<float>(400000);
        static IceResurfacerGame game;
        static IceMachineAudio audio;
        static MethodInfo animation,update;
        static IEnumerator routine;
        static Keyboard keyboard;
        static Mouse mouse;
        static InputSettings originalInput,validationInput;
        static bool recording,collect,oldPause;
        static float oldListenerVolume;
        static int frame=-1,waitFrames,channels,oldCapture,errors;
        static double started,energy,leftEnergy,rightEnergy;
        static long samples;
        static float rms,leftRms,rightRms;

        static IceSpatialAudioValidation()
        {if(Application.isBatchMode&&SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void RunBatch()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Spatial audio recording requires the isolated validation project.");
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(IceResurfacerEditor.ScenePath);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        static void Bind(IceResurfacerGame owner)
        {
            game=owner;audio=game.GetComponent<IceMachineAudio>();
            animation=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            update=typeof(IceResurfacerGame).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        }
        static void Check(bool valid,string message)
        {if(!valid)throw new InvalidOperationException("Spatial audio: "+message);checks.Add("PASS "+message);}
        static AudioSource Source(string name)
        {
            foreach(var source in game.GetComponentsInChildren<AudioSource>())if(source.name==name)return source;
            throw new InvalidOperationException("Missing source "+name);
        }
        static AudioSource Layer(string name) {return Source("Machine audio / "+name);}
        static void Render() {animation.Invoke(game,new object[]{0f});}
        static void Position(float x=0,float z=0,float yaw=0)
        {
            var s=game.Sim;s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=yaw;
            s.Conditioner.X=x;s.Conditioner.Z=z-1.14f;s.Conditioner.Heading=yaw;Render();
            audio.SetState(s,true,false);
        }
        static void Near(Vector3 a,Vector3 b,string name) {Check(Vector3.Distance(a,b)<.0001f,name);}

        internal static void CheckStructure(IceResurfacerGame owner)
        {
            Bind(owner);checks.Clear();bool enabled=game.enabled;game.enabled=false;
            try
            {
                game.NewSession(true);Position();var w=game.World;var s=game.Sim;
                var sources=game.GetComponentsInChildren<AudioSource>();
                Check(sources.Length==27,"27 stable sources: 19 loops, 2 mechanism voices, 3 impact voices, 3 water-service voices");
                int ambient=0;
                foreach(var source in sources)
                {
                    bool arena=source==Layer("Arena");if(arena)ambient++;
                    Check(source.spatialBlend==(arena?0:1),source.name+" uses "+(arena?"2D arena ambience":"full 3D spatialization"));
                    if(!arena)Check(source.dopplerLevel==0&&source.rolloffMode==AudioRolloffMode.Custom&&
                        source.minDistance==2.5f&&source.maxDistance==120&&source.reverbZoneMix==0,
                        source.name+" shares world-meter falloff without camera-velocity pitch or wet 2D leakage");
                    if(source.clip!=null)Check(source.clip.channels==1,"original mono synthesized clip: "+source.clip.name);
                }
                Check(ambient==1,"only arena ventilation is a non-positional loop");
                var ids=new HashSet<int>();foreach(var source in sources)ids.Add(source.GetInstanceID());
                for(int i=0;i<70;i++)
                {
                    s.Vehicle.Heading=i*.019f;s.Vehicle.X=i*.012f;s.Vehicle.Z=i*.018f;
                    s.Conditioner.Heading=-i*.005f;s.Conditioner.Lift=(i%20)/25f;s.TankLift=(i%24)/30f;
                    Render();audio.SetState(s,true,i>0);
                    Near(Layer("Engine").transform.position,w.Machine.Find("Shaped engine cover / right of operator").position,"engine follows physical engine compartment "+i);
                    Near(Layer("Drive").transform.position,w.Machine.Find("Central steel chassis").position,"drivetrain follows central chassis "+i);
                    Near(Layer("Blade").transform.position,w.CuttingBlade.TransformPoint(0,-.5f,0),"scrape follows actual blade edge "+i);
                    Near(Layer("Horizontal").transform.position,w.HorizontalAuger.position,"horizontal auger follows independent conditioner "+i);
                    Near(Layer("Vertical").transform.position,w.VerticalAuger.TransformPoint(0,.21f,0),"vertical auger stays inside conveyor housing "+i);
                    Near(Layer("Flood").transform.position,w.WaterOutlet.position,"flood originates at actual distribution outlet "+i);
                    Near(Layer("ConditionerLeft").transform.position,w.ConditionerCylinderBarrels[0].position,"conditioner hydraulic follows actual moving barrel "+i);
                    Near(Layer("TankRight").transform.position,w.TankCylinderBarrels[1].position,"tank hydraulic follows actual moving barrel "+i);
                    Near(Layer("Dump").transform.position,w.DumpOutlet.position,"idle dump emitter follows raised-tank outlet "+i);
                    Near(Layer("Discharge").transform.position,w.TankDischarge.position,"snow discharge follows fixed conveyor outlet "+i);
                }
                foreach(var source in game.GetComponentsInChildren<AudioSource>())Check(ids.Remove(source.GetInstanceID()),"source is reused through movement and tank/lift animation");
                Check(ids.Count==0,"no source created or destroyed per frame");
                s.Controls.ConditionerHydraulic=s.Controls.TankHydraulic=1;
                audio.SetState(s,true,true);
                var targets=(float[])typeof(IceMachineAudio).GetField("target",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
                Check(Mathf.Abs(targets[3]+targets[4]+targets[5]+targets[6]-.22f)<.00001f,"split actuators retain the original aggregate hydraulic level");
                s.Controls.FloodFlow=.6f;s.Controls.WashValve=.4f;s.Controls.WashPump=true;audio.SetState(s,true,false);
                Check(Mathf.Abs(targets[7]+targets[8]+targets[9]-(.6f*.12f+.4f*.09f+.06f))<.00001f,"split flood / wash / pickup retain independent state responses and original summed level");
                Check(Mathf.Abs(Layer("Engine").pitch-(.76f+Mathf.Abs(s.Controls.Throttle)*.12f+Mathf.Abs(s.Vehicle.Speed)*.075f+Mathf.Clamp01(s.MachineLoad)*.08f))<.00001f,"engine pitch retains throttle/speed/load response");
                SnowAnchors();ServiceAnchors();ContactAnchor();
                Check(IceAudioSpatialization.GainAt(2)==1&&Mathf.Abs(IceAudioSpatialization.GainAt(10)-.25f)<.00001f&&
                    IceAudioSpatialization.GainAt(40)>.05f&&IceAudioSpatialization.GainAt(120)==0,
                    "falloff has a 2.5 m near field, meaningful 10/40 m sound and a distant 120 m fade");
            }
            finally
            {
                Directory.CreateDirectory(Root);File.WriteAllLines(Root+"Structure-results.txt",checks);
                game.NewSession(true);game.enabled=enabled;
            }
        }
        static void ServiceAnchors()
        {
            game.NewSession(true);var s=game.Sim;
            Check(s.ConfigureWaterForServiceTest(.5f,.5f)&&s.TryDismount()&&s.TakeHose(),"real practice fill setup");
            foreach(var port in new[]{WaterFillPort.Flood,WaterFillPort.Wash})
            {
                Check(s.ConnectHose(port,5)&&s.SetSupplyValve(1),"connect actual "+port+" inlet and open supply");
                s.Step(.02f);Render();game.WaterService.UpdateViewAndEffects(true,.02f);
                var inlet=port==WaterFillPort.Flood?game.WaterService.FloodInlet:game.WaterService.WashInlet;
                Near(Source("Service audio / Tank filling").transform.position,inlet.position,"fill audio follows connected "+port+" inlet");
                s.SetSupplyValve(0);Check(s.DisconnectHose(),"disconnect fill fixture");
            }
            Check(s.SetSupplyValve(1),"loose pressurized nozzle fixture");s.Step(.02f);game.WaterService.UpdateViewAndEffects(true,.02f);
            Near(Source("Service audio / Actual spill").transform.position,game.WaterService.OverflowParticles.transform.position,"spill audio and visible water have identical actual origins");
            s.SetSupplyValve(0);
            game.NewSession(true);s=game.Sim;
            Check(s.TryDismount()&&s.TakeHose()&&s.ConnectHose(WaterFillPort.Flood,5)&&s.SetSupplyValve(1),"connected full-tank overflow fixture");
            s.Step(.02f);Render();game.WaterService.UpdateViewAndEffects(true,.02f);
            Check(s.ServiceOverflowLitersPerSecond>0,"actual full tank produces overflow flow");
            Near(Source("Service audio / Actual spill").transform.position,game.WaterService.OverflowParticles.transform.position,
                "connected overflow audio follows the real standpipe spill, distinct from the fill inlet");
            s.SetSupplyValve(0);
        }
        static void SnowAnchors()
        {
            game.NewSession(true);Position();var s=game.Sim;
            typeof(Simulation).GetProperty("HorizontalSnowM3").SetValue(s,s.Config.ConditionerSnowCapacityM3+.02f);
            s.Step(.02f);Render();audio.SetState(s,true,false);
            Check(s.ConditionerLeftEscapeRate>0&&s.ConditionerRightEscapeRate>0,"finite trough overflow produces both actual side exits");
            Near(Layer("EscapeLeft").transform.position,new Vector3(s.ConditionerLeftExitX,game.World.Conditioner.TransformPoint(0,.16f,0).y,s.ConditionerLeftExitZ),"left snow audio matches actual side-exit particle origin");
            Near(Layer("EscapeRight").transform.position,new Vector3(s.ConditionerRightExitX,game.World.Conditioner.TransformPoint(0,.16f,0).y,s.ConditionerRightExitZ),"right snow audio matches actual side-exit particle origin");
            s.Controls.VerticalAuger=true;s.TankLift=.7f;
            typeof(Simulation).GetProperty("VerticalSnowM3").SetValue(s,.02f);
            s.Step(.02f);Render();audio.SetState(s,true,false);
            Check(s.VerticalSpillRate>0,"raised-bin rejection produces actual upper snow spill");
            Near(Layer("Upper").transform.position,new Vector3(s.UpperSnowOriginX,s.UpperSnowOriginY,s.UpperSnowOriginZ),"upper snow audio matches actual rejection outlet");
            typeof(Simulation).GetProperty("SnowTankM3").SetValue(s,.15f);s.TankLift=1;
            s.Step(.02f);Render();audio.SetState(s,true,false);
            Check(s.DumpSnowRate>0,"actual snow unload flow exists in dump fixture");
            Near(Layer("Dump").transform.position,new Vector3(s.DumpOriginX,s.DumpOriginY,s.DumpOriginZ),"dump audio follows the same moving outlet as actual snow discharge");
            var targets=(float[])typeof(IceMachineAudio).GetField("target",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
            float sum=0;for(int i=14;i<19;i++)sum+=targets[i];
            float expected=Mathf.Clamp01((s.DumpSnowRate+s.DischargeSnowRate+s.EscapingSnowRate)*10)*.27f+Mathf.Clamp01(s.ConveyorCongestion)*Mathf.Clamp01(Mathf.Abs(s.Vehicle.Speed))*.06f;
            Check(Mathf.Abs(sum-expected)<.00001f,"split snow outlets preserve the original aggregate snow volume response");
        }
        static void ContactAnchor()
        {
            game.NewSession(true);Position(0,26);var s=game.Sim;
            s.Vehicle.Speed=2;s.Controls.Throttle=.5f;
            for(int i=0;i<2500&&s.ContactEventSequence==0;i++)s.Step(.02f);
            Check(s.ContactEventSequence>0,"actual board collision produces a contact event");Render();audio.SetState(s,true,false);
            var point=new Vector3(s.AudibleContact.PointX,s.AudibleContact.PointY,s.AudibleContact.PointZ);
            bool positioned=false;for(int i=0;i<3;i++)positioned|=Vector3.Distance(Source("Physical contact voice "+i).transform.position,point)<.00001f;
            Check(positioned,"collision voice originates at the exact detected world contact, including height");
        }

        static void Input(params UnityEngine.InputSystem.Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());
            InputSystem.Update();update.Invoke(game,null);
        }
        static void ListenerModes()
        {
            game.NewSession(true);Position();int listener=game.DriverCamera.GetComponent<AudioListener>().GetInstanceID();
            var ids=new HashSet<int>();foreach(var source in game.GetComponentsInChildren<AudioSource>())ids.Add(source.GetInstanceID());
            foreach(var mode in new[]{"Seated","Standing","Chase","OnFoot"})
            {
                if(mode=="Standing")Input(UnityEngine.InputSystem.Key.R);
                if(mode=="Chase"){Input(UnityEngine.InputSystem.Key.R);Input();}
                if(mode=="OnFoot"){Input();for(int i=0;i<30;i++)Input();Input(UnityEngine.InputSystem.Key.F);}
                float prior=IceAudioSpatialization.GainAt(Vector3.Distance(Layer("Engine").transform.position,game.DriverCamera.transform.position));
                for(int i=0;i<70;i++)
                {
                    Input(mode=="Chase"?new[]{UnityEngine.InputSystem.Key.C}:new UnityEngine.InputSystem.Key[0]);
                    float gain=IceAudioSpatialization.GainAt(Vector3.Distance(Layer("Engine").transform.position,game.DriverCamera.transform.position));
                    Check(Mathf.Abs(gain-prior)<.35f,"continuous physical distance gain during "+mode+" transition "+i);prior=gain;
                }
                var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
                int active=0;foreach(var current in listeners)if(current.isActiveAndEnabled){active++;Check(current.GetInstanceID()==listener,"same original listener in "+mode);}
                Check(active==1&&game.DriverCamera.GetComponent<AudioListener>().enabled,"exactly one active camera listener in "+mode);
                if(mode=="Standing")Check(game.Standing,"actual R standing input used");
                if(mode=="Chase")Check(game.ChaseActive,"actual held C chase input used");
                if(mode=="OnFoot")Check(game.Sim.OnFoot,"actual F dismount input used");
            }
            var before=game.WaterService.FootPosition;
            for(int i=0;i<150;i++)Input(UnityEngine.InputSystem.Key.W);Input();
            Check(Vector3.Distance(before,game.WaterService.FootPosition)>2,"listener follows actual on-foot WASD walking while machinery runs");
            foreach(var source in game.GetComponentsInChildren<AudioSource>())Check(ids.Remove(source.GetInstanceID()),"camera/input modes reuse source instance");
            Check(ids.Count==0,"no mode-specific audio sources are added or lost");
        }

        static void Tick()
        {
            if(!EditorApplication.isPlaying||Time.frameCount==frame)return;frame=Time.frameCount;
            if(++waitFrames<25)return;
            try
            {
                if(routine==null)
                {
                    started=EditorApplication.timeSinceStartup;Directory.CreateDirectory(Root);
                    Bind(UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>());
                    Check(game!=null&&game.Sim!=null,"saved launch scene initialized for offline DSP capture");
                    game.enabled=false;oldCapture=Time.captureFramerate;Time.captureFramerate=60;
                    originalInput=InputSystem.settings;validationInput=UnityEngine.Object.Instantiate(originalInput);
                    validationInput.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                    validationInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=validationInput;
                    keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
                    oldPause=AudioListener.pause;oldListenerVolume=AudioListener.volume;AudioListener.pause=false;AudioListener.volume=1;
                    Application.logMessageReceived+=Log;routine=Run();
                }
                if(EditorApplication.timeSinceStartup-started>600)throw new TimeoutException("Spatial audio validation timed out.");
                bool more=routine.MoveNext();
                if(recording)CaptureFrame();
                if(!more)Finish(0);
            }
            catch(Exception e){checks.Add("FAIL "+e);Debug.LogException(e);Finish(1);}
        }
        static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        static IEnumerator Run()
        {
            CheckStructure(game);game.enabled=false;ListenerModes();
            game.NewSession(true);game.Sim.Conditioner.Lift=0;Position(-9,-20);
            for(int i=0;i<80;i++)Input();
            Vector3 seat=game.DriverCamera.transform.position;Quaternion seatRotation=game.DriverCamera.transform.rotation;
            Input(UnityEngine.InputSystem.Key.R);for(int i=0;i<60;i++)Input();
            Vector3 standing=game.DriverCamera.transform.position;Quaternion standRotation=game.DriverCamera.transform.rotation;
            Input(UnityEngine.InputSystem.Key.R);Input();
            var configuration=AudioSettings.GetConfiguration();
            Check(configuration.speakerMode==AudioSpeakerMode.Stereo,"stereo DSP output available for orientation checks");
            channels=2;checks.Add("DSP sample rate="+AudioSettings.outputSampleRate+", real voices="+configuration.numRealVoices+", virtual voices="+configuration.numVirtualVoices);
            Check(configuration.numRealVoices>=27,"fixed source budget fits the existing real-voice configuration");
            recording=AudioRenderer.Start();Check(recording,"Unity AudioRenderer records the real spatialized master mix offline");
            rows.Clear();rows.Add("probe,source,distance_m,curve_gain,measured_gain,relative_db,rms_combined,left_rms,right_rms,sample_frames");
            foreach(string layer in new[]{"Engine","Flood"})
            {
                var source=Layer(layer);foreach(var other in game.GetComponentsInChildren<AudioSource>())other.mute=other!=source;
                float reference=0;
                foreach(string pose in new[]{"2m","seat","standing","10m","across_width","opposite_end"})
                {
                    Vector3 point=source.transform.position;
                    float dy=1.65f-point.y;
                    Vector3 position=pose=="seat"?seat:pose=="standing"?standing:
                        pose=="across_width"?new Vector3(11,1.65f,-20):pose=="opposite_end"?new Vector3(-9,1.65f,25):
                        point+new Vector3(Mathf.Sqrt(Mathf.Max(0,(pose=="2m"?4:100)-dy*dy)),dy,0);
                    Quaternion rotation=pose=="seat"?seatRotation:pose=="standing"?standRotation:Quaternion.LookRotation(point-position);
                    game.DriverCamera.transform.SetPositionAndRotation(position,rotation);
                    var measure=Measure(layer+"-"+pose,source);while(measure.MoveNext())yield return null;
                    if(pose=="2m")reference=rms;
                    float distance=Vector3.Distance(point,position),expected=IceAudioSpatialization.GainAt(distance),gain=rms/reference;
                    Check(Mathf.Abs(gain-expected)<Mathf.Max(.015f,expected*.12f),layer+" "+pose+" DSP gain="+gain.ToString("F4")+" expected="+expected.ToString("F4")+" at "+distance.ToString("F3")+"m");
                    Row(layer+"-"+pose,source,gain);
                }
            }
            var engine=Layer("Engine");foreach(var source in game.GetComponentsInChildren<AudioSource>())source.mute=source!=engine;
            engine.spatialBlend=0;float legacyNear=0;
            foreach(float distance in new[]{2f,25f})
            {
                game.DriverCamera.transform.position=engine.transform.position+Vector3.back*distance;
                game.DriverCamera.transform.rotation=Quaternion.identity;
                var measure=Measure("legacy-2D-"+distance+"m",engine);while(measure.MoveNext())yield return null;
                if(distance==2)legacyNear=rms;
                Row("legacy-2D-"+distance+"m",engine,rms/legacyNear);
                if(distance==25)Check(Mathf.Abs(rms/legacyNear-1)<.05f,"legacy 2D reference reproduces distance-independent output");
            }
            engine.spatialBlend=1;
            float rightBefore=0,leftBefore=0;
            foreach(bool turned in new[]{false,true})
            {
                game.DriverCamera.transform.position=engine.transform.position+Vector3.left*5;
                game.DriverCamera.transform.rotation=Quaternion.Euler(0,turned?180:0,0);
                var measure=Measure(turned?"pan-left-after-head-turn":"pan-right",engine);while(measure.MoveNext())yield return null;
                if(!turned){Check(rightRms>leftRms*1.25f,"source on listener right is louder in right channel");rightBefore=rightRms;leftBefore=leftRms;}
                else Check(leftRms>rightRms*1.25f&&Mathf.Abs(leftRms-rightBefore)<rightBefore*.1f,
                    "180 degree head turn swaps stereo orientation without an artificial level jump");
                Row(turned?"pan-left-after-head-turn":"pan-right",engine,1);
            }
            Check(errors==0,"no Unity audio/runtime errors during offline capture");
            checks.Add("PASS measured audio is native Unity DSP output, not only the configured distance curve. WAV files retain absolute levels; no per-file normalization.");
        }
        static IEnumerator Measure(string name,AudioSource source)
        {
            source.Stop();source.volume=.5f;source.pitch=1;source.timeSamples=0;source.Play();
            for(int i=0;i<20;i++)yield return null;
            energy=leftEnergy=rightEnergy=0;samples=0;pcm.Clear();collect=true;
            for(int i=0;i<180;i++)yield return null;
            collect=false;Check(samples>AudioSettings.outputSampleRate,"DSP capture contains at least one second: "+name);
            rms=(float)Math.Sqrt(energy/samples);leftRms=(float)Math.Sqrt(leftEnergy/samples);rightRms=(float)Math.Sqrt(rightEnergy/samples);
            Check(rms>.000001f,"non-silent spatialized output: "+name);
            WriteWav(Root+name+".wav");
        }
        static void CaptureFrame()
        {
            int count=AudioRenderer.GetSampleCountForCaptureFrame();
            using(var buffer=new NativeArray<float>(count*channels,Allocator.Temp))
            {
                if(!AudioRenderer.Render(buffer))throw new InvalidOperationException("Unity DSP render failed.");
                if(!collect)return;
                for(int i=0;i<buffer.Length;i+=channels)
                {
                    float left=buffer[i],right=buffer[i+1];leftEnergy+=left*left;rightEnergy+=right*right;energy+=left*left+right*right;
                    pcm.Add(left);pcm.Add(right);
                }
                samples+=count;
            }
        }
        static void Row(string name,AudioSource source,float gain)
        {
            float distance=Vector3.Distance(source.transform.position,game.DriverCamera.transform.position);
            rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:F4},{3:F6},{4:F6},{5:F3},{6:F8},{7:F8},{8:F8},{9}",
                name,source.name,distance,source.spatialBlend==0?1:IceAudioSpatialization.GainAt(distance),gain,20*Math.Log10(Math.Max(.0000001,gain)),rms,leftRms,rightRms,samples));
        }
        static void WriteWav(string path)
        {
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                int bytes=pcm.Count*2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)channels);writer.Write(AudioSettings.outputSampleRate);
                writer.Write(AudioSettings.outputSampleRate*channels*2);writer.Write((short)(channels*2));writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
                foreach(float sample in pcm)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
            }
        }
        static void Finish(int exitCode)
        {
            if(recording){AudioRenderer.Stop();recording=false;}
            Time.captureFramerate=oldCapture;AudioListener.pause=oldPause;AudioListener.volume=oldListenerVolume;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(originalInput!=null)InputSystem.settings=originalInput;if(validationInput!=null)UnityEngine.Object.DestroyImmediate(validationInput);
            Application.logMessageReceived-=Log;EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
            Directory.CreateDirectory(Root);checks.Add("Runtime error count: "+errors);
            File.WriteAllLines(Root+"Results.txt",checks);File.WriteAllLines(Root+"Measured-falloff.csv",rows);
            Debug.Log("SPATIAL AUDIO VALIDATION "+(exitCode==0?"PASS":"FAIL")+"; checks="+checks.Count+"; errors="+errors);
            EditorApplication.Exit(exitCode==0&&errors==0?0:1);
        }
    }
}
