using System;
using System.IO;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    // Explicit tools. Never switches scenes, enters play, or edits project settings on import.
    [InitializeOnLoad]
    public static class IceResurfacerEditor
    {
        public const string ScenePath="Assets/Experiments/IceResurfacer/Scenes/IceResurfacer.unity";
        const string Root="Assets/Experiments/IceResurfacer/";
        const string BatchKey="IceResurfacer.BatchValidation";
        static int frame;
        static int failures;
        static double began;
        static bool passPrepared;
        static bool layoutStarted;
        static string resultReport;
        static Keyboard testKeyboard;
        static Mouse testMouse;
        static Quaternion releasedHead;
        static InputSettings originalInputSettings, validationInputSettings;

        static IceResurfacerEditor()
        {
            if(Application.isBatchMode && SessionState.GetBool(BatchKey,false))
            {
                EditorApplication.playModeStateChanged+=PlayState;
                Application.logMessageReceived+=Log;
                if(EditorApplication.isPlaying) { began=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; }
            }
        }
        [MenuItem("Experiments/Ice Resurfacer/Open Launch Scene")]
        public static void Open()
        {
            if(EditorApplication.isPlaying)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Experiments/Ice Resurfacer/Validate Assets")]
        public static void ValidateAssets()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Resources/AuroraLit.mat");
            var ice=AssetDatabase.LoadAssetAtPath<Material>(Root+"Resources/NorthlineIce.mat");
            Require(material!=null&&ice!=null,"Both owned materials exist");
            Require(material.shader!=null&&ice.shader!=null,"Material shader references resolve");
            Require(!ShaderUtil.ShaderHasError(ice.shader),"Spatial ice shader compiles");
            Require(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(Root+"Resources/WorldText.shader")),"World text shader compiles");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null,"Saved scene imports");
            var sim=new Core.Simulation(mode:Core.SimulationMode.GuidedTraining);
            Require(sim.Ice.IceCellCount>30000,"Spatial ice grid initialized");
            Require(!sim.TryFinish(),"Fresh session cannot finish before entering rink");
            Debug.Log("ICE VALIDATION: assets and initial simulation passed. Use standalone validation for mechanics.");
        }
        // Invoke explicitly with -batchmode -executeMethod IceResurfacer.Editor.IceResurfacerEditor.RunBatch.
        // No -quit: this method exits after real Play Mode + graphics checks.
        public static void RunBatch()
        {
            try
            {
                ValidateAssets();
                Directory.CreateDirectory(Root+"Validation~");
                SessionState.SetBool(BatchKey,true);
                EditorSceneManager.OpenScene(ScenePath);
                EditorApplication.playModeStateChanged+=PlayState;
                Application.logMessageReceived+=Log;
                EditorApplication.EnterPlaymode();
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        // Development-player framebuffer validation uses only the isolated project copy.
        // Its temporary scene and player settings are never written into the user's open project.
        public static void BuildLayoutPlayer()
        {
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                    throw new InvalidOperationException("BuildLayoutPlayer requires the isolated StageUnityValidation.ps1 project copy.");
                EditorSceneManager.OpenScene(ScenePath);
                new GameObject("Explicit layout validation probe").AddComponent<IceLayoutPlayerProbe>();
                string validationScene=Root+"Scenes/LayoutValidationPlayer.unity";
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),validationScene);
                // Keep disposable player preferences separate from the real project's product.
                PlayerSettings.companyName="Aurora Prototype Validation";
                PlayerSettings.productName="Aurora Layout Validation";
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
                PlayerSettings.defaultScreenWidth=1366;PlayerSettings.defaultScreenHeight=768;
                PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
                string output=Path.GetFullPath("../Build/AuroraLayout.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{validationScene},locationPathName=output,
                    target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
                });
                if(result.summary.result!=BuildResult.Succeeded)
                    throw new InvalidOperationException("Layout player build: "+result.summary.result);
                Debug.Log("AURORA LAYOUT PLAYER BUILT: "+output);
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)failures++;
        }
        static void PlayState(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode)
            { frame=0; began=EditorApplication.timeSinceStartup; EditorApplication.update-=Tick; EditorApplication.update+=Tick; }
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            if(EditorApplication.timeSinceStartup-began>600) { Fail("Play mode validation timed out"); return; }
            try
            {
                frame++;
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                if(game==null||game.Sim==null) { if(frame>200)Fail("Launch scene did not initialize"); return; }
                if(frame==30)
                {
                    Require(game.DriverCamera!=null&&game.World.IceMesh!=null,"Camera and surface spawned");
                    Require(game.GetComponentsInChildren<AudioSource>().Length==27,"Nineteen machine/ambience loops, two mechanisms, three contact voices and three service sources initialize");
                    Require(game.World.Conditioner!=game.World.Machine,"Independent conditioner transform");
                    Require(game.DriverCamera.transform.IsChildOf(game.World.CameraMount),"Seated camera attached");
                    Capture(game.DriverCamera,"Driver-start.png");
                    game.NewSession(false,true);
                    // Fast deterministic setup of a genuine treated straight lane. No state-texture painting.
                    var sim=game.Sim; sim.Controls.HydraulicHoldControls=false; sim.Controls.Throttle=-.65f;
                    for(int i=0;i<1000;i++)sim.Step(.02f);
                    // Rendering fixture uses a centred lane after validating the new
                    // entrance drive. The independent full-run test never sets poses.
                    Require(sim.HasEnteredRink,"Southeast gate admits actual driven entry");
                    sim.Vehicle.X=0;sim.Vehicle.Z=-22;sim.Vehicle.Heading=0;sim.Vehicle.Speed=0;
                    sim.Conditioner.X=0;sim.Conditioner.Z=-23.1f;sim.Conditioner.Heading=0;
                    sim.Controls.Throttle=.42f;
                    sim.Controls.ConditionerTarget=0;
                    sim.Controls.HorizontalAuger=true; sim.Controls.VerticalAuger=true;
                    for(int i=0;i<110;i++)sim.Step(.02f);
                    sim.Controls.BladeDepthMm=.35f; sim.Controls.WashValve=.25f;
                    for(int i=0;i<510;i++)sim.Step(.02f);
                    sim.Controls.WashPump=true; sim.Controls.FloodFlow=.32f;
                    for(int i=0;i<700;i++)sim.Step(.02f);
                    sim.Controls.Throttle=0; sim.Controls.FloodFlow=0; sim.Controls.WashValve=0; sim.Controls.WashPump=false;
                    Require(sim.GetReport().Coverage>.02f,"Actual trailing footprint processes spatial cells");
                    Require(sim.SnowTankM3>.005f,"Shaved snow reaches tank");
                    game.Surface.Refresh(sim); passPrepared=true;
                }
                if(frame==62&&passPrepared)
                {
                    // Headless Editor has no focused Game View. Route only this test's
                    // virtual devices through a transient settings clone, never the project asset.
                    originalInputSettings=InputSystem.settings;
                    validationInputSettings=UnityEngine.Object.Instantiate(originalInputSettings);
                    validationInputSettings.hideFlags=HideFlags.HideAndDontSave;
                    validationInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    validationInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings=validationInputSettings;
                    testKeyboard=InputSystem.AddDevice<Keyboard>();
                    testMouse=InputSystem.AddDevice<Mouse>();
                    InputSystem.QueueStateEvent(testMouse,new MouseState { buttons=2,delta=new Vector2(-900,250) });
                }
                if(frame==75&&passPrepared)
                {
                    Require(game.Looking,"RMB input enters head-look mode");
                    Require(Quaternion.Angle(Quaternion.Euler(5,0,0),game.DriverCamera.transform.localRotation)>20,"Mouse motion rotates the head");
                    releasedHead=game.DriverCamera.transform.localRotation;
                    InputSystem.QueueStateEvent(testMouse,new MouseState());
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.W,Key.RightShift));
                }
                if(frame==68&&passPrepared)
                    InputSystem.QueueStateEvent(testMouse,new MouseState { buttons=2,delta=new Vector2(-900,250) });
                if(frame==95&&passPrepared)
                {
                    Require(!game.Looking,"RMB release restores instrument mode");
                    Require(Vector3.Distance(game.DriverCamera.transform.localRotation*Vector3.forward,releasedHead*Vector3.forward)<.000001f,"Released head keeps exactly the same vehicle-relative orientation");
                    Require(Mathf.Abs(game.Sim.Controls.Throttle-.42f)<.001f,"W + Right Shift reaches precision pedal demand");
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.LeftCtrl));
                }
                if(frame==102&&passPrepared)
                {
                    Require(game.Sim.Controls.Brake,"Ctrl input reaches service brake");
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
                }
                if(frame==120&&passPrepared)
                {
                    // Fix the preview poses after input checks; batch updates can run
                    // much faster than wall time, before head easing has fully settled.
                    game.DriverCamera.transform.localRotation=Quaternion.Euler(5,0,0);
                    game.DriverCamera.transform.localPosition=Vector3.zero;
                    Capture(game.DriverCamera,"Driver-running.png");
                    // Driver's rear/down view: inspect conditioner and real dark/wet trail.
                    game.DriverCamera.transform.localRotation=Quaternion.Euler(55,155,0);
                    game.DriverCamera.transform.localPosition=new Vector3(.52f,.03f,0);
                    Capture(game.DriverCamera,"Conditioner-trail.png");
                    var cameraObject=new GameObject("Validation overview camera");
                    var camera=cameraObject.AddComponent<Camera>();
                    camera.transform.position=new Vector3(10,8.3f,-26);
                    camera.transform.LookAt(new Vector3(0,0,0)); camera.fieldOfView=65;
                    camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.05f,.09f,.12f);
                    Capture(camera,"Arena-overview.png"); camera.enabled=false;UnityEngine.Object.Destroy(cameraObject);
                    Require(!ShaderUtil.ShaderHasError(game.World.IceMesh.sharedMaterial.shader),"Live ice shader has no compilation errors");
                    int renderers=game.GetComponentsInChildren<Renderer>().Length;
                    resultReport="Unity "+Application.unityVersion+" Play Mode validation\n"+
                        "Saved launch scene, camera, world, conditioner, state textures, twenty-seven audio sources: PASS\n"+
                        "Genuine simulated cut/wash/flood lane and snow conveyance: PASS\n"+
                        "Input System: persistent RMB motion/release, W+RightShift precision pedal, Ctrl brake: PASS\n"+
                        "Runtime renderers: "+renderers+"\n"+
                        "Graphics device: "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\n"+
                        "Runtime error/exception count: "+failures+"\n"+
                        "Camera inspection and physical-layout checks; HUD framebuffer and GUI input validated separately in the development player.\n";
                }
                if(frame==125&&passPrepared)
                {
                    IceLayoutValidation.Begin(game);layoutStarted=true;
                }
                if(layoutStarted&&IceLayoutValidation.Tick())
                {
                    Suite("Camera and sightlines",()=>IceCameraValidation.Run(game,testKeyboard,testMouse));
                    Suite("Snow operation rendering",()=>IceOperationValidation.Run(game));
                    Suite("Input and drive recovery",()=>IceAuthenticityValidation.Run(game,testKeyboard,testMouse));
                    Suite("Physical controls",()=>IcePhysicalValidation.Run(game,testKeyboard,testMouse));
                    Suite("Cockpit refinement",()=>IceCockpitRefinementValidation.Run(game,testKeyboard,testMouse));
                    Suite("Southeast service and freedom",()=>IceServiceValidation.Run(game,testKeyboard,testMouse));
                    Suite("Ice overlap / pickup rendering",()=>IceCalibrationRuntimeValidation.Run(game));
                    Suite("Spatial machine audio",()=>IceSpatialAudioValidation.CheckStructure(game));
                    Suite("Training route editing and advisory guidance",()=>IceTrainingGuideValidation.Run(game,testKeyboard,testMouse));
                    Suite("Route recording and persistence",()=>IceRouteRecordingValidation.Run(game,testKeyboard,testMouse));
                    resultReport+="Final runtime error/exception count: "+failures+"\n";
                    File.WriteAllText(Root+"Validation~/Unity-results.txt",resultReport);
                    Debug.Log("ICE UNITY SMOKE COMPLETE\n"+resultReport);
                    RestoreInput();
                    SessionState.SetBool(BatchKey,false); EditorApplication.update-=Tick;
                    EditorApplication.Exit(failures==0?0:1);
                }
            }
            catch(Exception e) { Fail(e.ToString()); }
        }
        internal static void Capture(Camera camera,string name,int width=1440,int height=900)
        {
            // URP single-camera request works without changing scene or pipeline settings.
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var old=camera.targetTexture;var previous=RenderTexture.active;
            Texture2D texture=null;
            try
            {
                target.Create();camera.targetTexture=target;camera.Render();
                RenderTexture.active=target;
                texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes(Root+"Validation~/"+name,texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=old;RenderTexture.active=previous;
                // Suites capture many frames during one Editor update; deferred
                // Destroy retains all of those native textures until it ends.
                if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
                target.Release();UnityEngine.Object.DestroyImmediate(target);
            }
        }
        static void Suite(string name,Action run)
        {
            try{run();resultReport+=name+": PASS\n";}
            catch(Exception e){resultReport+=name+": FAIL "+e.Message+"\n";Debug.LogError(name+": "+e);}
        }
        static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        static void RestoreInput()
        {
            if(testKeyboard!=null)InputSystem.RemoveDevice(testKeyboard);
            if(testMouse!=null)InputSystem.RemoveDevice(testMouse);
            if(originalInputSettings!=null)InputSystem.settings=originalInputSettings;
            if(validationInputSettings!=null)UnityEngine.Object.Destroy(validationInputSettings);
        }
        static void Fail(string message)
        {
            RestoreInput();
            SessionState.SetBool(BatchKey,false); EditorApplication.update-=Tick;
            File.WriteAllText(Root+"Validation~/Unity-results.txt","FAIL: "+message);
            Debug.LogError("ICE VALIDATION FAILED: "+message); EditorApplication.Exit(1);
        }
    }
}
