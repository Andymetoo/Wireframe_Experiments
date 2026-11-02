using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>The only UI that can replace authored assets: explanation first, typed
    /// confirmation, verified backup, automatic rollback. Closing the window changes nothing.</summary>
    public sealed class IceRecoveryWindow : EditorWindow
    {
        static readonly string[] Modes={"Replace machine","Replace arena section","Restore backup"};
        int mode, section, backup;
        bool resetMaterials;
        string typed="";
        Vector2 scroll;

        public static void Open()
        {
            var window=GetWindow<IceRecoveryWindow>(true,"Aurora / Northline recovery");
            window.minSize=new Vector2(560,520);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Advanced recovery. Nothing here runs automatically. To get a clean baseline without touching your work, "+
                "use Advanced Recovery > Generate Fresh ... Copy instead: it writes a new folder under "+IceAuthoredRecovery.RecoveryFolder+".",MessageType.Warning);
            int previous=mode;
            mode=GUILayout.Toolbar(mode,Modes);
            if(mode!=previous){typed="";resetMaterials=false;}
            IceAuthoredRecovery.Plan plan=null;string problem=null;
            try
            {
                if(mode==0)
                {
                    resetMaterials=EditorGUILayout.ToggleLeft("Also reset the machine materials to procedural values",resetMaterials);
                    plan=IceAuthoredRecovery.PlanMachine(resetMaterials);
                }
                else if(mode==1)
                {
                    section=EditorGUILayout.Popup("Section",section,IceArenaBaker.Sections);
                    resetMaterials=EditorGUILayout.ToggleLeft("Also reset ALL shared arena materials (affects every section)",resetMaterials);
                    if(!File.Exists(IceArenaBaker.SectionPrefabPath(IceArenaBaker.ArenaFolder,IceArenaBaker.Sections[section])))
                        problem="There is no authored arena yet. Use Authored Scene > Set Up Persistent Arena.";
                    else plan=IceAuthoredRecovery.PlanArenaSection(IceArenaBaker.Sections[section],resetMaterials);
                }
                else
                {
                    var backups=IceAuthoredRecovery.Backups();
                    if(backups.Count==0)problem="No backups in "+IceAuthoredRecovery.BackupFolder+" yet.";
                    else
                    {
                        var names=backups.ConvertAll(Path.GetFileName).ToArray();
                        backup=Mathf.Clamp(EditorGUILayout.Popup("Backup",backup,names),0,names.Length-1);
                        plan=IceAuthoredRecovery.PlanRestore(backups[backup]);
                    }
                }
            }
            catch(Exception e){problem=e.Message;}

            scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.ExpandHeight(true));
            if(problem!=null)EditorGUILayout.HelpBox(problem,MessageType.Info);
            if(plan!=null)EditorGUILayout.SelectableLabel(plan.Explanation(),EditorStyles.wordWrappedLabel,GUILayout.ExpandHeight(true),GUILayout.MinHeight(300));
            EditorGUILayout.EndScrollView();
            if(plan==null)return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("To continue, type "+plan.Phrase+" exactly:",EditorStyles.boldLabel);
            typed=EditorGUILayout.TextField(typed);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Cancel"))Close();
                using(new EditorGUI.DisabledScope(typed.Trim()!=plan.Phrase||EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button("Back up, then "+(mode==2?"restore":"replace")))
                    {
                        try
                        {
                            string folder=IceAuthoredRecovery.Execute(plan,typed);
                            EditorUtility.DisplayDialog("Done",plan.Title+".\n\nBackup of the previous files:\n"+folder,"OK");
                            Close();
                        }
                        catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("Not completed",e.Message,"OK");}
                        GUIUtility.ExitGUI();
                    }
            }
        }
    }
}
