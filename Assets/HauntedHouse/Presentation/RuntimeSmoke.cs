using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace HollowHouse
{
    /// <summary>Opt-in player validation, enabled only with -houseSmoke path in a development build.</summary>
    public sealed class RuntimeSmoke : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        HauntedHouseGame game;
        string output;
        int errors;
#if UNITY_EDITOR
        public static void RunInEditor(bool mapOnly=false)
        {
            var runner=new GameObject("Automated editor walkthrough").AddComponent<RuntimeSmoke>();
            runner.output=Path.GetFullPath("Logs/EditorSmoke");
            Directory.CreateDirectory(runner.output);
            if(!mapOnly) File.WriteAllText(Path.Combine(runner.output,"smoke-result.txt"),"");
            runner.StartCoroutine(mapOnly ? runner.RunMap() : runner.Run());
        }
        IEnumerator RunMap()
        {
            yield return null;
            game=FindFirstObjectByType<HauntedHouseGame>();
            game.NewCase(128);
            for(int room=0;room<game.Session.Scenario.Rooms.Count;room++) {yield return Walk(room);game.Session.Inspect();Counter();}
            game.OpenPanel("map");yield return Capture("09-tall-house-map");
            UnityEditor.EditorApplication.isPlaying=false;
        }
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)
                if(args[i]=="-houseSmoke")
                {
                    if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=VectorCabinet.GameCollection.HouseScene)
                        VectorCabinet.GameCollection.Open(VectorCabinet.GameCollection.HouseScene);
                    var runner=new GameObject("Automated player walkthrough").AddComponent<RuntimeSmoke>();
                    runner.output=Path.GetFullPath(args[i+1]);
                    Directory.CreateDirectory(runner.output);
                    File.WriteAllText(Path.Combine(runner.output,"smoke-result.txt"),"");
                    runner.StartCoroutine(runner.Run());
                    return;
                }
        }
        void Record(string message,string trace,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)
            { errors++; File.AppendAllText(Path.Combine(output,"runtime-errors.txt"),message+"\n"+trace+"\n"); }
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.35f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
            yield return new WaitForSecondsRealtime(.2f);
        }
        void Refresh() {game.House.SetView(game.Session.Position,game.Session.Facing);}
        void Counter()
        {
            var s=game.Session;
            if(s.ThreatCountdown<=0) return;
            if(s.Scenario.Archetype==Archetype.Mourner && s.Lantern) s.ToggleLantern();
            if(s.Scenario.Archetype==Archetype.Watcher) s.TurnFacing((int)s.ThreatDirection-(int)s.Facing);
            s.Wait(); Refresh();
        }
        IEnumerator Walk(int room)
        {
            var s=game.Session;
            var path=s.Scenario.FindPath(s.Position,s.Scenario.Rooms[room].Center);
            for(int i=1;i<path.Count;i++)
            {
                Counter();
                var delta=path[i]-s.Position;
                int facing=delta.X>0 ? 1 : delta.X<0 ? 3 : delta.Y>0 ? 0 : 2;
                s.TurnFacing(facing-(int)s.Facing);
                var cell=s.Scenario.Cells[path[i]];
                if(cell.DoorId>=0 && !s.DoorsOpen[cell.DoorId]) {s.InteractDoor();Counter();s.TurnFacing(facing-(int)s.Facing);}
                s.Move(0); Refresh();
                yield return null;
            }
            Counter();
        }
        IEnumerator Run()
        {
            Application.runInBackground=true;
            Application.logMessageReceived+=Record;
            yield return null;
            game=FindFirstObjectByType<HauntedHouseGame>();
            if(game==null) {File.WriteAllText(Path.Combine(output,"smoke-result.txt"),"FAIL: no composition root");Application.Quit(1);yield break;}
            yield return Capture("01-title");
            for(int kind=0;kind<3;kind++)
            {
                int seed=0;
                while(ScenarioGenerator.Generate(seed).Archetype!=(Archetype)kind) seed++;
                game.NewCase(seed);
                var s=game.Session;
                yield return Capture("02-"+s.Scenario.Archetype+"-entrance");
                for(int r=0;r<s.Scenario.Rooms.Count;r++)
                {
                    yield return Walk(r);
                    s.Inspect();Counter();
                    if(r==4)
                    {
                        yield return Capture("03-"+s.Scenario.Archetype+"-room");
                        game.OpenPanel("journal");yield return Capture("04-"+s.Scenario.Archetype+"-journal");game.ClosePanel();
                        game.OpenPanel("map");yield return Capture("05-"+s.Scenario.Archetype+"-map");game.ClosePanel();
                    }
                }
                yield return Walk(s.Scenario.Relics[s.Scenario.AnchorRelic].RoomId);
                s.TakeRelic();Counter();
                if(kind!=0)
                    foreach(int relay in s.Scenario.RelayRooms) {yield return Walk(relay);s.Attune();Counter();}
                yield return Walk(s.Scenario.OriginRoom);
                s.PlaceRelic();Counter();
                if(kind==0) {if(s.Lantern)s.ToggleLantern();s.Wait();s.Wait();s.Wait();}
                if(kind==1) s.TurnFacing((int)s.Scenario.RitualFacing-(int)s.Facing);
                if(kind==2) {s.Communicate();Counter();}
                s.Release();Refresh();
                if(s.Ending!=Ending.Won) {errors++;File.AppendAllText(Path.Combine(output,"runtime-errors.txt"),"Walkthrough failed "+s.Scenario.Archetype+" "+s.Ending+"\n"+string.Join("\n",s.Log));}
                yield return Capture("06-"+s.Scenario.Archetype+"-resolved");
                File.AppendAllText(Path.Combine(output,"smoke-result.txt"),s.Scenario.Archetype+": "+s.Ending+", "+s.Turn+" actions, "+s.Health+" health\n");
            }
            // Inspect a telegraphed manifestation and its terminal failure screen in the renderer.
            game.NewCase(10);
            var dangerCase=game.Session;
            yield return Walk(dangerCase.Scenario.OriginRoom);
            for(int i=0;i<150 && dangerCase.ThreatCountdown==0;i++)
            {
                dangerCase.TurnFacing((int)dangerCase.ThreatDirection+2-(int)dangerCase.Facing);
                dangerCase.Communicate();
            }
            dangerCase.TurnFacing((int)dangerCase.ThreatDirection-(int)dangerCase.Facing);Refresh();
            yield return Capture("07-watcher-warning");
            for(int i=0;i<200 && dangerCase.Ending==Ending.Investigating;i++)
            {
                dangerCase.TurnFacing((int)dangerCase.ThreatDirection+2-(int)dangerCase.Facing);
                dangerCase.Communicate();
            }
            Refresh();yield return Capture("08-defeat");
            if(dangerCase.Ending!=Ending.Lost) errors++;
            File.AppendAllText(Path.Combine(output,"smoke-result.txt"),"Reckless Watcher: "+dangerCase.Ending+"\n");
            game.NewCase(128);
            for(int room=0;room<game.Session.Scenario.Rooms.Count;room++) {yield return Walk(room);game.Session.Inspect();Counter();}
            game.OpenPanel("map");yield return Capture("09-tall-house-map");game.ClosePanel();
            yield return Walk(0);game.Session.Escape();yield return Capture("10-withdrawal");
            File.AppendAllText(Path.Combine(output,"smoke-result.txt"),"Withdrawal: "+game.Session.Ending+"\n");
            File.AppendAllText(Path.Combine(output,"smoke-result.txt"),"Runtime errors: "+errors+"\n");
            Application.logMessageReceived-=Record;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit(errors==0?0:1);
#endif
        }
#endif
    }
}
