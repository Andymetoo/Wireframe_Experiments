using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Authored-scene commands and the only code that may replace authored assets.
    /// Everyday: open the authored scene. First-time setup: create what does not exist yet.
    /// Advanced Recovery: generate fresh copies at new locations (never destructive), or, after a
    /// typed confirmation, replace an authored asset in place behind a verified backup with
    /// automatic rollback. Nothing here runs on import, domain reload, Play or Inspector edits.
    /// </summary>
    public static class IceAuthoredRecovery
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        public const string RecoveryFolder=Root+"Recovery";
        public const string BackupFolder=Root+"Backups~";
        public const string MachinePhrase="REBUILD MACHINE", SectionPhrase="REBUILD ARENA SECTION", RestorePhrase="RESTORE BACKUP";
        const string Menu="Experiments/Ice Resurfacer/";
        // Validation only: throw after this many file writes to prove rollback (-1 disables).
        internal static int FailAfterWrites=-1;

        // ---- Everyday ---------------------------------------------------------------------

        [MenuItem(Menu+"Open Authored Scene")]
        static void OpenAuthoredScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(IceMachineBaker.AuthoredScenePath)!=null)
            { EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);return; }
            if(EditorUtility.DisplayDialog("First-time setup: authored scene",IceMachineBaker.AuthoredScenePath+" does not exist.\n\n"+
                "Create it now as a copy of the launch scene (same calibration)? The launch scene itself is not changed.","Create authored scene","Cancel"))
                Report(() => IceMachineBaker.OpenOrCreateAuthoredScene());
        }

        [MenuItem(Menu+"Authored Scene/Set Up Persistent Arena (first time only)")]
        static void SetUpArena()
        {
            Report(() =>
            {
                var game=IceMachineBaker.AuthoredGame();
                if(!EditorUtility.DisplayDialog("First-time setup: persistent arena",
                    "Generates the rink and arena once from the procedural builder into the new folder "+IceArenaBaker.ArenaFolder+
                    " (section prefabs, meshes, materials) and adds one instance to "+IceMachineBaker.AuthoredScenePath+".\n\n"+
                    "The scene file is backed up to "+BackupFolder+" first. The machine prefab, its meshes and materials, recordings and guides are not touched.",
                    "Generate arena","Cancel"))return;
                Selection.activeGameObject=IceArenaBaker.SetUpFirstArena(game);
            });
        }
        [MenuItem(Menu+"Authored Scene/Set Up Persistent Arena (first time only)",true)]
        static bool CanSetUpArena() { return !EditorApplication.isPlaying&&!AssetDatabase.IsValidFolder(IceArenaBaker.ArenaFolder); }

        // ---- Advanced Recovery: non-destructive -------------------------------------------

        [MenuItem(Menu+"Advanced Recovery/Generate Fresh Machine Copy From Procedural Source")]
        static void FreshMachine()
        {
            Report(() =>
            {
                var game=IceMachineBaker.SceneGame(SceneManager.GetActiveScene());
                string folder=IceAssetPersistence.UniqueFolder(RecoveryFolder,"Machine");
                var prefab=IceMachineBaker.GenerateAssets(game,folder);
                EditorGUIUtility.PingObject(prefab);
                EditorUtility.DisplayDialog("Fresh machine copy","Generated a new baseline machine in\n"+folder+"\n\n"+
                    "Your authored machine ("+IceMachineBaker.PrefabPath+"), its materials and the scene were not changed. "+
                    "Compare or copy parts from the fresh copy, or delete the folder when done.","OK");
            });
        }

        [MenuItem(Menu+"Advanced Recovery/Generate Fresh Arena Copy From Procedural Source")]
        static void FreshArena()
        {
            Report(() =>
            {
                var game=IceMachineBaker.SceneGame(SceneManager.GetActiveScene());
                string folder=IceAssetPersistence.UniqueFolder(RecoveryFolder,"Arena");
                var prefab=IceArenaBaker.Generate(game,folder,null);
                EditorGUIUtility.PingObject(prefab);
                EditorUtility.DisplayDialog("Fresh arena copy","Generated a new baseline arena in\n"+folder+"\n\n"+
                    "Your authored arena ("+IceArenaBaker.ArenaFolder+") and the scene were not changed.","OK");
            });
        }

        [MenuItem(Menu+"Advanced Recovery/Set Up Machine (first time only)")]
        static void SetUpMachine()
        {
            Report(() =>
            {
                var game=IceMachineBaker.AuthoredGame();
                if(!EditorUtility.DisplayDialog("First-time setup: machine","Generates the machine into "+IceMachineBaker.MachineFolder+
                    " and adds one instance to the authored scene.","Generate machine","Cancel"))return;
                Selection.activeGameObject=IceMachineBaker.SetUpFirstMachine(game);
            });
        }
        [MenuItem(Menu+"Advanced Recovery/Set Up Machine (first time only)",true)]
        static bool CanSetUpMachine() { return !EditorApplication.isPlaying&&AssetDatabase.LoadMainAssetAtPath(IceMachineBaker.PrefabPath)==null; }

        [MenuItem(Menu+"Advanced Recovery/Replace or Restore Authored Assets...")]
        static void OpenWindow() { IceRecoveryWindow.Open(); }

        static void Report(Action action)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorUtility.DisplayDialog("Ice Resurfacer","Exit Play Mode first.","OK");return;}
            try { action(); }
            catch(Exception e) { Debug.LogException(e);EditorUtility.DisplayDialog("Ice Resurfacer",e.Message,"OK"); }
        }

        // ---- Destructive replacement (typed confirmation, backup, rollback) ---------------

        public sealed class Plan
        {
            public string Title, Phrase;
            public readonly List<string> Replaced=new List<string>();
            public readonly List<string> Notes=new List<string>();
            internal Func<IceResurfacerGame,string,Dictionary<string,byte[]>> Prepare;
            internal Action Verify;
            public string Explanation()
            {
                var text=new StringBuilder();
                text.AppendLine(Title).AppendLine();
                text.AppendLine("Files replaced in place (their GUIDs and .meta files are kept, so references survive):");
                foreach(string path in Replaced)text.AppendLine("  "+path);
                text.AppendLine();
                foreach(string note in Notes)text.AppendLine(note);
                text.AppendLine();
                text.AppendLine("Order of operations: (1) the fresh version is generated into a temporary staging folder and checked; if that fails, nothing is changed. "+
                    "(2) every affected file, its .meta and the authored scene are copied to "+BackupFolder+"/<time> and verified byte for byte. "+
                    "(3) the files are written; if any write or the final check fails, all of them are restored from that backup automatically.");
                text.AppendLine("Recovery afterwards: this window's Restore tab, or close Unity and copy the backup folder's files back into "+Root+" (same relative paths).");
                return text.ToString();
            }
        }

        public static Plan PlanMachine(bool resetMaterials)
        {
            var plan=new Plan{Title="Replace the authored machine with a fresh procedural machine",Phrase=MachinePhrase};
            plan.Replaced.Add(IceMachineBaker.PrefabPath);
            plan.Replaced.Add(IceMachineBaker.MeshAssetPath);
            if(resetMaterials)plan.Replaced.AddRange(Assets(IceMachineBaker.MaterialFolder,"*.mat"));
            plan.Notes.Add("Prefab: every edit saved in the prefab (moved controls and mounts, replaced or deleted parts, Inspector values) is replaced by the procedural machine.");
            plan.Notes.Add(resetMaterials?"Materials: the machine materials listed above are reset to their procedural colours and settings.":
                "Materials: kept exactly as they are (your colour edits survive); the fresh prefab uses them by name.");
            plan.Notes.Add("Scene: "+IceMachineBaker.AuthoredScenePath+" keeps its link to the prefab. Overrides saved in the scene on individual machine parts no longer apply (they point at regenerated objects).");
            plan.Notes.Add("Not touched: the arena, recordings, guides, the launch scene.");
            plan.Prepare=(game,staging) =>
            {
                var prefab=IceMachineBaker.GenerateAssets(game,staging);
                prefab.GetComponent<IceMachineRig>().Collect();
                return Content(staging+"/"+IceMachineBaker.PrefabName,IceMachineBaker.PrefabPath,
                    new[]{(staging+"/"+IceMachineBaker.MeshAssetName,IceMachineBaker.MeshAssetPath)},staging+"/Materials",IceMachineBaker.MaterialFolder,resetMaterials);
            };
            plan.Verify=() =>
            {
                var game=IceMachineBaker.AuthoredGame();
                var rig=game.GetComponentInChildren<IceMachineRig>(true);
                if(rig==null||PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.gameObject)!=IceMachineBaker.PrefabPath)
                    throw new InvalidOperationException("The authored scene lost its machine prefab instance.");
                rig.Collect();
            };
            return plan;
        }

        public static Plan PlanArenaSection(string section,bool resetMaterials)
        {
            var plan=new Plan{Title="Replace arena section '"+section+"' with a fresh procedural section",Phrase=SectionPhrase};
            plan.Replaced.AddRange(IceArenaBaker.SectionFiles(IceArenaBaker.ArenaFolder,section));
            if(resetMaterials)plan.Replaced.AddRange(Assets(IceArenaBaker.MaterialFolder,"*.mat"));
            plan.Notes.Add("Section prefab: every edit saved in this section's prefab is replaced. The other "+(IceArenaBaker.Sections.Length-1)+" sections are not touched.");
            plan.Notes.Add(resetMaterials?"Materials: ALL shared arena materials listed above are reset to procedural values; this changes every section that uses them.":
                "Materials: the shared arena materials are kept exactly as they are; the fresh section uses them by name.");
            plan.Notes.Add("Scene and arena prefab: they keep their link to this section (same GUID). Overrides saved on this section's individual parts in the arena prefab or scene no longer apply.");
            plan.Notes.Add("Not touched: the machine, recordings, guides, the launch scene.");
            plan.Prepare=(game,staging) =>
            {
                IceArenaBaker.GenerateSection(game,section,staging,resetMaterials?null:IceArenaBaker.MaterialFolder);
                var files=new List<(string,string)>();
                if(File.Exists(IceArenaBaker.SectionMeshPath(staging,section)))files.Add((IceArenaBaker.SectionMeshPath(staging,section),IceArenaBaker.SectionMeshPath(IceArenaBaker.ArenaFolder,section)));
                return Content(IceArenaBaker.SectionPrefabPath(staging,section),IceArenaBaker.SectionPrefabPath(IceArenaBaker.ArenaFolder,section),
                    files.ToArray(),staging+"/Materials",IceArenaBaker.MaterialFolder,resetMaterials);
            };
            plan.Verify=() =>
            {
                var game=IceMachineBaker.AuthoredGame();
                var rig=game.GetComponentInChildren<IceArenaRig>(true);
                if(rig==null)throw new InvalidOperationException("The authored scene lost its arena.");
                foreach(var finding in IceArenaDrift.Evaluate(rig,game.calibration))
                    if(float.IsInfinity(finding.Error))throw new InvalidOperationException("Replaced arena is missing a part: "+finding.Label);
            };
            return plan;
        }

        public static Plan PlanRestore(string backup)
        {
            var manifest=Manifest.Read(backup);
            var plan=new Plan{Title="Restore the backup '"+Path.GetFileName(backup)+"' ("+manifest.label+")",Phrase=RestorePhrase};
            foreach(var entry in manifest.files)if(!entry.path.EndsWith(".meta"))plan.Replaced.Add(entry.path);
            plan.Notes.Add("Every file above (and its .meta) is put back exactly as it was when the backup was made, including the authored scene.");
            plan.Notes.Add("The current state is backed up first, so this restore can itself be undone the same way.");
            plan.Prepare=(game,staging) =>
            {
                var content=new Dictionary<string,byte[]>();
                foreach(var entry in manifest.files)
                {
                    byte[] bytes=File.ReadAllBytes(BackupCopyPath(backup,entry.path));
                    if(Hash(bytes)!=entry.sha256)throw new InvalidOperationException("Backup file changed since it was made: "+entry.path);
                    content[entry.path]=bytes;
                }
                return content;
            };
            plan.Verify=() => { };
            return plan;
        }

        /// <summary>Runs a plan. Throws without changing anything unless typed equals the plan's phrase.
        /// Returns the backup folder.</summary>
        public static string Execute(Plan plan,string typed)
        {
            if((typed??"").Trim()!=plan.Phrase)throw new OperationCanceledException("Confirmation text did not match '"+plan.Phrase+"'. Nothing was changed.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first. Nothing was changed.");
            if(UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Close Prefab Mode first. Nothing was changed.");
            var game=IceMachineBaker.AuthoredGame();
            if(game.gameObject.scene.isDirty)throw new InvalidOperationException("Save the authored scene first. Nothing was changed.");

            string staging=IceAssetPersistence.UniqueFolder(RecoveryFolder,"Staging");
            Dictionary<string,byte[]> content;
            try { content=plan.Prepare(game,staging); }
            catch(Exception e) { DeleteStaging(staging);throw new InvalidOperationException("Generating the fresh version failed; nothing was changed. "+e.Message,e); }

            var touched=new List<string>(content.Keys);
            if(!touched.Contains(IceMachineBaker.AuthoredScenePath))touched.Add(IceMachineBaker.AuthoredScenePath);
            string backup;
            try { backup=BackUp(plan.Phrase,touched); }
            catch(Exception e) { DeleteStaging(staging);throw new InvalidOperationException("The backup could not be verified; nothing was changed. "+e.Message,e); }
            // Staged assets are removed before writing, so any GUID they carry is free again.
            DeleteStaging(staging);

            var written=new List<string>();
            try
            {
                foreach(var entry in content)
                {
                    if(FailAfterWrites>=0&&written.Count>=FailAfterWrites)throw new IOException("Injected validation failure after "+written.Count+" writes");
                    written.Add(entry.Key);
                    WriteAtomically(entry.Key,entry.Value);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                plan.Verify();
            }
            catch(Exception e)
            {
                RollBack(backup,written);
                throw new InvalidOperationException("Replacement failed and was rolled back from "+backup+": "+e.Message,e);
            }
            Debug.Log("AURORA RECOVERY: "+plan.Title+". Backup: "+backup);
            return backup;
        }

        // Fresh content for live paths: the staged prefab with staged GUIDs remapped to the live
        // assets, the staged meshes written over the live container (its GUID kept), and staged
        // materials either remapped to live ones by name or, when resetting, written over them.
        static Dictionary<string,byte[]> Content(string stagedPrefab,string livePrefab,(string staged,string live)[] files,string stagedMaterials,string liveMaterials,bool resetMaterials)
        {
            var content=new Dictionary<string,byte[]>();
            var remap=new Dictionary<string,string>();
            foreach(var file in files)
            {
                if(File.Exists(file.live)){remap[Guid(file.staged)]=Guid(file.live);content[file.live]=File.ReadAllBytes(file.staged);}
                else{content[file.live]=File.ReadAllBytes(file.staged);content[file.live+".meta"]=File.ReadAllBytes(file.staged+".meta");}
            }
            if(Directory.Exists(stagedMaterials))
                foreach(string staged in Assets(stagedMaterials,"*.mat"))
                {
                    string live=liveMaterials+"/"+Path.GetFileName(staged);
                    if(File.Exists(live))
                    {
                        remap[Guid(staged)]=Guid(live);
                        if(resetMaterials)content[live]=File.ReadAllBytes(staged);
                    }
                    else{content[live]=File.ReadAllBytes(staged);content[live+".meta"]=File.ReadAllBytes(staged+".meta");}
                }
            string text=File.ReadAllText(stagedPrefab);
            foreach(var pair in remap)text=text.Replace("guid: "+pair.Key,"guid: "+pair.Value);
            content[livePrefab]=new UTF8Encoding(false).GetBytes(text);
            return content;
        }
        static string Guid(string path)
        {
            string guid=AssetDatabase.AssetPathToGUID(path);
            if(string.IsNullOrEmpty(guid))throw new InvalidOperationException("No GUID for "+path);
            return guid;
        }
        static List<string> Assets(string folder,string pattern)
        {
            var list=new List<string>();
            if(Directory.Exists(folder))foreach(string file in Directory.GetFiles(folder,pattern))list.Add(file.Replace('\\','/'));
            list.Sort(StringComparer.Ordinal);
            return list;
        }
        static void DeleteStaging(string staging)
        {
            if(AssetDatabase.IsValidFolder(staging))AssetDatabase.DeleteAsset(staging);
            else if(Directory.Exists(staging)) { Directory.Delete(staging,true);if(File.Exists(staging+".meta"))File.Delete(staging+".meta"); }
            if(Directory.Exists(RecoveryFolder)&&Directory.GetFileSystemEntries(RecoveryFolder).Length==0)
            {
                if(AssetDatabase.IsValidFolder(RecoveryFolder))AssetDatabase.DeleteAsset(RecoveryFolder);
                else { Directory.Delete(RecoveryFolder);if(File.Exists(RecoveryFolder+".meta"))File.Delete(RecoveryFolder+".meta"); }
            }
        }
        static void WriteAtomically(string path,byte[] bytes)
        {
            string directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            string temp=Path.Combine("Temp","aurora-recovery-"+System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory("Temp");
            File.WriteAllBytes(temp,bytes);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        static void RollBack(string backup,List<string> written)
        {
            var manifest=Manifest.Read(backup);
            var saved=new HashSet<string>();
            foreach(var entry in manifest.files)
            {
                WriteAtomically(entry.path,File.ReadAllBytes(BackupCopyPath(backup,entry.path)));
                saved.Add(entry.path);
            }
            // Files that did not exist before the replacement are removed again.
            foreach(string path in written)if(!saved.Contains(path)&&File.Exists(path))File.Delete(path);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(IceMachineBaker.AuthoredScenePath)!=null)EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
        }

        // ---- Backups ----------------------------------------------------------------------

        [Serializable] sealed class Manifest
        {
            public string label, created;
            public List<Entry> files=new List<Entry>();
            [Serializable] public sealed class Entry { public string path, sha256; }
            public static Manifest Read(string folder)
            {
                string file=Path.Combine(folder,"manifest.json");
                if(!File.Exists(file))throw new InvalidOperationException("Not a recovery backup (no manifest.json): "+folder);
                return JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
            }
        }

        /// <summary>Copies files and their .meta into Backups~/&lt;time&gt; &lt;label&gt;, verifies every copy
        /// byte for byte and writes a manifest and restore instructions. Returns the folder.</summary>
        public static string BackUp(string label,IEnumerable<string> paths)
        {
            Directory.CreateDirectory(BackupFolder);
            string folder=BackupFolder+"/"+DateTime.Now.ToString("yyyy-MM-dd HHmmss")+" "+IceAssetPersistence.Safe(label);
            for(int i=2;Directory.Exists(folder);i++)folder=BackupFolder+"/"+DateTime.Now.ToString("yyyy-MM-dd HHmmss")+" "+IceAssetPersistence.Safe(label)+" "+i;
            var manifest=new Manifest{label=label,created=DateTime.Now.ToString("s")};
            var copied=new HashSet<string>();
            foreach(string path in paths)
                foreach(string file in path.EndsWith(".meta")?new[]{path}:new[]{path,path+".meta"})
                {
                    if(!File.Exists(file)||!copied.Add(file))continue;
                    string target=BackupCopyPath(folder,file);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file,target);
                    string original=Hash(File.ReadAllBytes(file)),copy=Hash(File.ReadAllBytes(target));
                    if(original!=copy)throw new IOException("Backup copy differs from "+file);
                    manifest.files.Add(new Manifest.Entry{path=file.Replace('\\','/'),sha256=original});
                }
            if(manifest.files.Count==0)throw new InvalidOperationException("Nothing to back up.");
            File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonUtility.ToJson(manifest,true));
            File.WriteAllText(Path.Combine(folder,"RESTORE.txt"),
                "Aurora / Northline recovery backup: "+label+"\nMade "+manifest.created+"\n\n"+
                "Restore inside Unity: Experiments > Ice Resurfacer > Advanced Recovery > Replace or Restore Authored Assets... > Restore backup.\n"+
                "Restore by hand: close Unity, then copy every file in this folder (keeping its relative path, including .meta files) over "+Root+"\n\n"+
                "Files (path relative to the project, SHA-256):\n"+string.Join("\n",manifest.files.ConvertAll(e => "  "+e.path+"  "+e.sha256))+"\n");
            return folder;
        }
        /// <summary>Where a project file is kept inside a backup (relative to the experiment, keeping paths short).</summary>
        public static string BackupCopyPath(string backup,string file)
        {
            file=file.Replace('\\','/');
            return Path.Combine(backup,file.StartsWith(Root)?file.Substring(Root.Length):file);
        }
        public static List<string> Backups()
        {
            var list=new List<string>();
            if(Directory.Exists(BackupFolder))
                foreach(string folder in Directory.GetDirectories(BackupFolder))
                    if(File.Exists(Path.Combine(folder,"manifest.json")))list.Add(folder.Replace('\\','/'));
            list.Sort(StringComparer.Ordinal);list.Reverse();
            return list;
        }
        static string Hash(byte[] bytes)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
        }
    }
}
